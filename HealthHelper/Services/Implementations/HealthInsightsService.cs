using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Configuration;
using HealthHelper.Models;
using HealthHelper.Services.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace HealthHelper.Services.Implementations;

public sealed class HealthInsightsService : IHealthInsightsService, IDisposable
{
    private readonly ISystemClock _systemClock;
    private readonly IRecommendationClient _recommendationClient;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private const int HistoryWindow = 7;
    private bool _disposed;

    public HealthInsightsService(
        ISystemClock systemClock,
        IRecommendationClient recommendationClient,
        IOptions<HealthInsightsOptions> optionsAccessor)
    {
        _systemClock = systemClock;
        _recommendationClient = recommendationClient;

        var options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(optionsAccessor));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        InitializeDatabase();
    }

    public async Task SaveSnapshotAsync(DailySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> GetStoredDayCountAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM daily_logs;";
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AdviceBundle> GenerateCombinedAdviceAsync(
        DailySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await PersistSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);
            var recent = await LoadRecentSnapshotsAsync(cancellationToken).ConfigureAwait(false);
            var narrative = await _recommendationClient.CreateNarrativeAsync(snapshot, recent, cancellationToken)
                .ConfigureAwait(false);

            return new AdviceBundle(narrative);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DailySnapshot>> GetHistoricalSnapshotsAsync(int days = 7, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = new List<DailySnapshot>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT log_date,
                       bed_time,
                       wake_time,
                       sleep_quality,
                       hydration_target,
                       hydration_consumed,
                       workout_minutes,
                       sedentary_minutes,
                       advice_narrative
                FROM daily_logs
                ORDER BY log_date DESC
                LIMIT {days};
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(ReadSnapshot(reader));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DailySnapshot>> GetHistoricalSnapshotsPagedAsync(int pageSize, int skip, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = new List<DailySnapshot>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT log_date,
                       bed_time,
                       wake_time,
                       sleep_quality,
                       hydration_target,
                       hydration_consumed,
                       workout_minutes,
                       sedentary_minutes,
                       advice_narrative
                FROM daily_logs
                ORDER BY log_date DESC
                LIMIT {pageSize} OFFSET {skip};
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(ReadSnapshot(reader));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HealthTip> GetRandomHealthTipAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 首先尝试获取收藏的小贴士，如果有收藏的，优先显示
            await using var favoriteCommand = connection.CreateCommand();
            favoriteCommand.CommandText = """
                SELECT ht.id, ht.title, ht.content, ht.category, ht.created_at
                FROM health_tips ht
                INNER JOIN favorite_tips ft ON ht.id = ft.tip_id
                ORDER BY RANDOM()
                LIMIT 1;
                """;

            await using var favoriteReader = await favoriteCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await favoriteReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new HealthTip(
                    favoriteReader.GetInt32(0),
                    favoriteReader.GetString(1),
                    favoriteReader.GetString(2),
                    favoriteReader.GetString(3),
                    DateTimeOffset.Parse(favoriteReader.GetString(4), CultureInfo.InvariantCulture));
            }

            // 如果没有收藏的小贴士，随机选择一个普通的小贴士
            await using var randomCommand = connection.CreateCommand();
            randomCommand.CommandText = """
                SELECT id, title, content, category, created_at
                FROM health_tips
                ORDER BY RANDOM()
                LIMIT 1;
                """;

            await using var reader = await randomCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new HealthTip(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture));
            }

            // 如果没有数据，返回默认小贴士
            return new HealthTip(1, "保持健康的生活方式", "规律作息、多喝水、适量运动是保持身体健康的基础。", "general", _systemClock.Now);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<HealthTip>> GetAllHealthTipsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = new List<HealthTip>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, title, content, category, created_at
                FROM health_tips
                ORDER BY id;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new HealthTip(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsTipFavoritedAsync(int tipId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM favorite_tips WHERE tip_id = $tipId;";
            command.Parameters.AddWithValue("$tipId", tipId);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ToggleFavoriteAsync(int tipId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 检查是否已收藏
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(1) FROM favorite_tips WHERE tip_id = $tipId;";
            checkCommand.Parameters.AddWithValue("$tipId", tipId);

            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) > 0;

            if (exists)
            {
                // 取消收藏
                await using var deleteCommand = connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM favorite_tips WHERE tip_id = $tipId;";
                deleteCommand.Parameters.AddWithValue("$tipId", tipId);
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // 添加收藏
                await using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = "INSERT INTO favorite_tips (tip_id, favorited_at) VALUES ($tipId, $favoritedAt);";
                insertCommand.Parameters.AddWithValue("$tipId", tipId);
                insertCommand.Parameters.AddWithValue("$favoritedAt", _systemClock.Now.ToString("O"));
                await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<HealthTip>> GetFavoritedTipsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = new List<HealthTip>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ht.id, ht.title, ht.content, ht.category, ht.created_at
                FROM health_tips ht
                INNER JOIN favorite_tips ft ON ht.id = ft.tip_id
                ORDER BY ft.favorited_at DESC;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new HealthTip(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        if (RequiresBodyWeightMigration(connection))
        {
            MigrateAwayFromBodyWeight(connection);
        }

        if (RequiresAdviceNarrativeMigration(connection))
        {
            MigrateToAdviceNarrative(connection);
        }

        // 创建健康小贴士表
        using var tipsCommand = connection.CreateCommand();
        tipsCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS health_tips (
                id INTEGER PRIMARY KEY,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                category TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        tipsCommand.ExecuteNonQuery();

        // 创建收藏表
        using var favoritesCommand = connection.CreateCommand();
        favoritesCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS favorite_tips (
                tip_id INTEGER PRIMARY KEY,
                favorited_at TEXT NOT NULL,
                FOREIGN KEY (tip_id) REFERENCES health_tips (id)
            );
            """;
        favoritesCommand.ExecuteNonQuery();

        // 初始化健康小贴士数据
        InitializeHealthTips(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS daily_logs (
                log_date TEXT PRIMARY KEY,
                bed_time TEXT,
                wake_time TEXT,
                sleep_quality INTEGER,
                hydration_target REAL,
                hydration_consumed REAL,
                workout_minutes INTEGER,
                sedentary_minutes INTEGER,
                advice_narrative TEXT
            );
            """;
        command.ExecuteNonQuery();
    }

    private async Task PersistSnapshotAsync(DailySnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_logs (
                log_date,
                bed_time,
                wake_time,
                sleep_quality,
                hydration_target,
                hydration_consumed,
                workout_minutes,
                sedentary_minutes,
                advice_narrative)
            VALUES (
                $date,
                $bed,
                $wake,
                $sleepQuality,
                $hydrationTarget,
                $hydrationConsumed,
                $workoutMinutes,
                $sedentaryMinutes,
                $adviceNarrative)
            ON CONFLICT(log_date) DO UPDATE SET
                bed_time = excluded.bed_time,
                wake_time = excluded.wake_time,
                sleep_quality = excluded.sleep_quality,
                hydration_target = excluded.hydration_target,
                hydration_consumed = excluded.hydration_consumed,
                workout_minutes = excluded.workout_minutes,
                sedentary_minutes = excluded.sedentary_minutes,
                advice_narrative = excluded.advice_narrative;
            """;

        // 使用DBNull.Value处理NULL值
        command.Parameters.AddWithValue("$date", snapshot.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$bed", snapshot.Sleep?.BedTime.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$wake", snapshot.Sleep?.WakeTime.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sleepQuality", snapshot.Sleep?.QualityScore ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$hydrationTarget", snapshot.Hydration?.TargetMl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$hydrationConsumed", snapshot.Hydration?.ConsumedMl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$workoutMinutes", snapshot.Activity?.WorkoutMinutes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sedentaryMinutes", snapshot.Activity?.SedentaryMinutes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$adviceNarrative", snapshot.Advice?.Narrative ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await CleanupHistoryAsync(connection, snapshot.Date, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CleanupHistoryAsync(SqliteConnection connection, DateOnly pivot, CancellationToken cancellationToken)
    {
        var cutoff = pivot.AddDays(-(HistoryWindow - 1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await using var cleanupCommand = connection.CreateCommand();
        cleanupCommand.CommandText = "DELETE FROM daily_logs WHERE log_date < $cutoff;";
        cleanupCommand.Parameters.AddWithValue("$cutoff", cutoff);
        await cleanupCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<DailySnapshot>> LoadRecentSnapshotsAsync(CancellationToken cancellationToken)
    {
        var results = new List<DailySnapshot>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT log_date,
                   bed_time,
                   wake_time,
                   sleep_quality,
                   hydration_target,
                   hydration_consumed,
                   workout_minutes,
                   sedentary_minutes
            FROM daily_logs
            ORDER BY log_date DESC
            LIMIT {HistoryWindow};
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadSnapshot(reader));
        }

        results.Reverse();
        return results;
    }

    private static DailySnapshot ReadSnapshot(SqliteDataReader reader)
    {
        var date = DateOnly.Parse(reader.GetString(0), CultureInfo.InvariantCulture);

        SleepLog? sleep = null;
        if (!reader.IsDBNull(1) && !reader.IsDBNull(2) && !reader.IsDBNull(3))
        {
            var bed = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
            var wake = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
            var quality = reader.GetInt32(3);
            sleep = new SleepLog(bed, wake, quality);
        }

        HydrationLog? hydration = null;
        if (!reader.IsDBNull(4) && !reader.IsDBNull(5))
        {
            var target = reader.GetDouble(4);
            var consumed = reader.GetDouble(5);
            hydration = new HydrationLog(target, consumed);
        }

        ActivityLog? activity = null;
        if (!reader.IsDBNull(6) && !reader.IsDBNull(7))
        {
            var workout = reader.GetInt32(6);
            var sedentary = reader.GetInt32(7);
            activity = new ActivityLog(workout, sedentary);
        }

        AdviceBundle? advice = null;
        // 检查是否有advice_narrative字段（第8列）
        if (reader.FieldCount > 8 && !reader.IsDBNull(8))
        {
            var narrative = reader.GetString(8);
            advice = new AdviceBundle(narrative);
        }

        return new DailySnapshot(date, sleep, hydration, activity, advice);
    }

    private static bool RequiresBodyWeightMigration(SqliteConnection connection)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(daily_logs);";
        using var reader = checkCommand.ExecuteReader();
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (string.Equals(columnName, "body_weight", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void MigrateAwayFromBodyWeight(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        using (var rename = connection.CreateCommand())
        {
            rename.Transaction = transaction;
            rename.CommandText = "ALTER TABLE daily_logs RENAME TO daily_logs_legacy;";
            rename.ExecuteNonQuery();
        }

        using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = """
                CREATE TABLE daily_logs (
                    log_date TEXT PRIMARY KEY,
                    bed_time TEXT,
                    wake_time TEXT,
                    sleep_quality INTEGER,
                    hydration_target REAL,
                    hydration_consumed REAL,
                    workout_minutes INTEGER,
                    sedentary_minutes INTEGER
                );
                """;
            create.ExecuteNonQuery();
        }

        using (var copy = connection.CreateCommand())
        {
            copy.Transaction = transaction;
            copy.CommandText = """
                INSERT INTO daily_logs (
                    log_date,
                    bed_time,
                    wake_time,
                    sleep_quality,
                    hydration_target,
                    hydration_consumed,
                    workout_minutes,
                    sedentary_minutes)
                SELECT
                    log_date,
                    bed_time,
                    wake_time,
                    sleep_quality,
                    hydration_target,
                    hydration_consumed,
                    workout_minutes,
                    sedentary_minutes
                FROM daily_logs_legacy;
                """;
            copy.ExecuteNonQuery();
        }

        using (var drop = connection.CreateCommand())
        {
            drop.Transaction = transaction;
            drop.CommandText = "DROP TABLE daily_logs_legacy;";
            drop.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static bool RequiresAdviceNarrativeMigration(SqliteConnection connection)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(daily_logs);";
        using var reader = checkCommand.ExecuteReader();
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (string.Equals(columnName, "advice_narrative", StringComparison.OrdinalIgnoreCase))
            {
                return false; // 字段已存在，无需迁移
            }
        }

        return true; // 字段不存在，需要迁移
    }

    private static void MigrateToAdviceNarrative(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();

        // 为现有表添加新字段
        using (var alter = connection.CreateCommand())
        {
            alter.Transaction = transaction;
            alter.CommandText = "ALTER TABLE daily_logs ADD COLUMN advice_narrative TEXT;";
            alter.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void InitializeHealthTips(SqliteConnection connection)
    {
        // 检查是否已有数据
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(1) FROM health_tips;";
        var count = Convert.ToInt32(checkCommand.ExecuteScalar(), CultureInfo.InvariantCulture);

        if (count > 0)
        {
            return; // 已有数据，跳过初始化
        }

        var healthTips = new[]
        {
            (1, "规律作息很重要", "每天保持相同的作息时间，有助于调节生物钟，提高睡眠质量。建议晚上11点前入睡，早上6-7点起床。", "sleep"),
            (2, "多喝水保持身体水分", "成人每天应饮用至少2000ml水。早上起床后先喝一杯温水，有助于唤醒肠胃，促进新陈代谢。", "hydration"),
            (3, "适量运动强身健体", "每周进行150分钟的中等强度有氧运动，如快走、骑自行车，可以有效预防心血管疾病。", "exercise"),
            (4, "早餐要吃好", "早餐应包含蛋白质、碳水化合物和健康脂肪。建议食用全谷物、鸡蛋和水果，避免高糖食品。", "nutrition"),
            (5, "午休小憩有益健康", "中午小憩15-30分钟，可以缓解疲劳，提高下午工作效率。但避免睡得太久，以免影响晚上睡眠。", "rest"),
            (6, "健康零食选择", "选择坚果、水果、酸奶作为零食，避免高糖高脂的垃圾食品。每日坚果摄入量控制在30克以内。", "nutrition"),
            (7, "正确洗手预防疾病", "饭前便后、外出归来都要洗手。用肥皂和流动水洗手至少20秒，可以有效预防传染病。", "hygiene"),
            (8, "保持室内空气流通", "每天开窗通风至少2-3次，每次15-30分钟。保持室内空气新鲜，有助于预防呼吸道疾病。", "environment"),
            (9, "定期体检很重要", "建议每年进行一次全面体检，包括血常规、肝肾功能、心电图等。及早发现健康问题才能及早治疗。", "medical"),
            (10, "控制体重健康生活", "保持健康体重可以预防多种慢性病。BMI控制在18.5-24之间最适宜。", "weight"),
            (11, "健康坐姿保护脊椎", "坐姿要端正，腰部挺直，双脚平放地面。使用符合人体工程学的椅子，避免久坐不动。", "posture"),
            (12, "均衡膳食五色原则", "每天饮食应包含五种颜色的蔬菜水果：红色(番茄)、绿色(叶菜)、黄色(胡萝卜)、白色(大蒜)、紫色(茄子)。", "nutrition"),
            (13, "正确刷牙保护牙齿", "每天刷牙2-3次，每次2-3分钟。使用正确的刷牙方法，保护牙龈健康。", "oral"),
            (14, "眼保健操缓解眼疲劳", "长时间用眼后要做眼保健操，按摩眼部周围穴位，促进血液循环，缓解眼部疲劳。", "eyes"),
            (15, "适量饮茶有益健康", "适量饮用绿茶或乌龙茶，有助于抗氧化、降血脂。但避免过度饮用，以免影响睡眠。", "beverage"),
            (16, "健康睡眠环境", "卧室温度保持在18-22℃，湿度40-60%。使用舒适的床垫和枕头，保证睡眠质量。", "sleep"),
            (17, "戒烟限酒健康生活", "吸烟有害健康，建议完全戒烟。饮酒应适量，男性每日不超过25g酒精，女性不超过15g。", "lifestyle"),
            (18, "培养兴趣爱好", "培养健康的兴趣爱好，如阅读、音乐、园艺等，可以缓解压力，提高生活质量。", "mental"),
            (19, "定期清洁家居环境", "每周进行一次大扫除，清洁卫生死角。保持家居环境卫生，可以预防疾病传播。", "hygiene"),
            (20, "保持积极心态", "保持乐观积极的心态，多与家人朋友交流。良好的心态是健康的重要保障。", "mental")
        };

        var now = DateTimeOffset.Now.ToString("O");

        foreach (var (id, title, content, category) in healthTips)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT INTO health_tips (id, title, content, category, created_at)
                VALUES ($id, $title, $content, $category, $createdAt);
                """;
            insertCommand.Parameters.AddWithValue("$id", id);
            insertCommand.Parameters.AddWithValue("$title", title);
            insertCommand.Parameters.AddWithValue("$content", content);
            insertCommand.Parameters.AddWithValue("$category", category);
            insertCommand.Parameters.AddWithValue("$createdAt", now);
            insertCommand.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Dispose();
        _disposed = true;
    }
}