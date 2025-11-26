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

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        if (RequiresBodyWeightMigration(connection))
        {
            MigrateAwayFromBodyWeight(connection);
        }

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
                sedentary_minutes INTEGER
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
                sedentary_minutes)
            VALUES (
                $date,
                $bed,
                $wake,
                $sleepQuality,
                $hydrationTarget,
                $hydrationConsumed,
                $workoutMinutes,
                $sedentaryMinutes)
            ON CONFLICT(log_date) DO UPDATE SET
                bed_time = excluded.bed_time,
                wake_time = excluded.wake_time,
                sleep_quality = excluded.sleep_quality,
                hydration_target = excluded.hydration_target,
                hydration_consumed = excluded.hydration_consumed,
                workout_minutes = excluded.workout_minutes,
                sedentary_minutes = excluded.sedentary_minutes;
            """;

        command.Parameters.AddWithValue("$date", snapshot.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$bed", snapshot.Sleep?.BedTime.ToString("O"));
        command.Parameters.AddWithValue("$wake", snapshot.Sleep?.WakeTime.ToString("O"));
        command.Parameters.AddWithValue("$sleepQuality", snapshot.Sleep?.QualityScore);
        command.Parameters.AddWithValue("$hydrationTarget", snapshot.Hydration?.TargetMl);
        command.Parameters.AddWithValue("$hydrationConsumed", snapshot.Hydration?.ConsumedMl);
        command.Parameters.AddWithValue("$workoutMinutes", snapshot.Activity?.WorkoutMinutes);
        command.Parameters.AddWithValue("$sedentaryMinutes", snapshot.Activity?.SedentaryMinutes);

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

        return new DailySnapshot(date, sleep, hydration, activity);
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


