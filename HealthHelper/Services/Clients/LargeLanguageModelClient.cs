using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Models;
using HealthHelper.Services.Contracts;

namespace HealthHelper.Services.Clients;

/// <summary>
/// Simulates a large language model integration. The implementation purposely touches
/// environment variables and network resources, making it inconvenient to unit test.
/// </summary>
public sealed class LargeLanguageModelClient : IRecommendationClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public LargeLanguageModelClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<string> CreateNarrativeAsync(
        DailySnapshot today,
        IReadOnlyList<DailySnapshot> historicalSnapshots,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Environment.GetEnvironmentVariable("HEALTHHELPER_LLM_ENDPOINT");
        var payload = JsonSerializer.Serialize(new
        {
            today = MapSnapshot(today),
            history = historicalSnapshots.Select(MapSnapshot).ToArray(),
            generatedAt = DateTimeOffset.Now
        });

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        // Offline fallback: build a narrative locally but keep the same prompt structure.
        var builder = new StringBuilder();
        builder.AppendLine("【今日重点】");
        builder.AppendLine(BuildTodayLine(today));
        builder.AppendLine();
        builder.AppendLine("【近七日趋势】");
        foreach (var snapshot in historicalSnapshots)
        {
            builder.AppendLine($"- {snapshot.Date:MM-dd}: {BuildTodayLine(snapshot)}");
        }
        builder.AppendLine();
        builder.AppendLine("【行动建议】");
        builder.AppendLine("1. 睡前 1 小时关闭屏幕，保持稳定作息。");
        builder.AppendLine("2. 将饮水分散到全天，避免晚间集中补水。");
        builder.AppendLine("3. 每小时起身活动 5 分钟，累计 150 分钟有氧。");
        return builder.ToString();
    }

    private static object MapSnapshot(DailySnapshot snapshot) => new
    {
        snapshot.Date,
        snapshot.BodyWeightKg,
        SleepHours = snapshot.Sleep?.Duration.TotalHours,
        snapshot.Sleep?.QualityScore,
        snapshot.Hydration?.TargetMl,
        snapshot.Hydration?.ConsumedMl,
        snapshot.Activity?.WorkoutMinutes,
        snapshot.Activity?.SedentaryMinutes
    };

    private static string BuildTodayLine(DailySnapshot snapshot)
    {
        var parts = new List<string>();
        if (snapshot.Sleep is not null)
        {
            parts.Add($"睡眠 {snapshot.Sleep.Duration.TotalHours:F1}h(Q{snapshot.Sleep.QualityScore})");
        }

        if (snapshot.Hydration is not null)
        {
            var ratio = snapshot.Hydration.TargetMl > 0
                ? snapshot.Hydration.ConsumedMl / snapshot.Hydration.TargetMl
                : 0;
            parts.Add($"饮水 {ratio:P0}");
        }

        if (snapshot.Activity is not null)
        {
            parts.Add($"运动 {snapshot.Activity.WorkoutMinutes}min");
        }

        return string.Join(" | ", parts);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}


