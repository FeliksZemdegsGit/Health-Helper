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

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS daily_logs (
                log_date TEXT PRIMARY KEY,
                body_weight REAL NOT NULL,
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
                body_weight,
                bed_time,
                wake_time,
                sleep_quality,
                hydration_target,
                hydration_consumed,
                workout_minutes,
                sedentary_minutes)
            VALUES (
                $date,
                $weight,
                $bed,
                $wake,
                $sleepQuality,
                $hydrationTarget,
                $hydrationConsumed,
                $workoutMinutes,
                $sedentaryMinutes)
            ON CONFLICT(log_date) DO UPDATE SET
                body_weight = excluded.body_weight,
              bed_time = excluded.bed_time,
              wake_time = excluded.wake_time,
              sleep_quality = excluded.sleep_quality,
              hydration_target = excluded.hydration_target,
              hydration_consumed = excluded.hydration_consumed,
              workout_minutes = excluded.workout_minutes,
              sedentary_minutes = excluded.sedentary_minutes;
            """;

        command.Parameters.AddWithValue("$date", snapshot.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$weight", snapshot.BodyWeightKg);
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
                   body_weight,
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
        var bodyWeight = reader.GetDouble(1);

        SleepLog? sleep = null;
        if (!reader.IsDBNull(2) && !reader.IsDBNull(3) && !reader.IsDBNull(4))
        {
            var bed = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
            var wake = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture);
            var quality = reader.GetInt32(4);
            sleep = new SleepLog(bed, wake, quality);
        }

        HydrationLog? hydration = null;
        if (!reader.IsDBNull(5) && !reader.IsDBNull(6))
        {
            var target = reader.GetDouble(5);
            var consumed = reader.GetDouble(6);
            hydration = new HydrationLog(target, consumed, bodyWeight);
        }

        ActivityLog? activity = null;
        if (!reader.IsDBNull(7) && !reader.IsDBNull(8))
        {
            var workout = reader.GetInt32(7);
            var sedentary = reader.GetInt32(8);
            activity = new ActivityLog(workout, sedentary);
        }

        return new DailySnapshot(date, bodyWeight, sleep, hydration, activity);
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


