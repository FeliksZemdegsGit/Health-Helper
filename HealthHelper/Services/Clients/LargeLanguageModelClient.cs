using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
            Timeout = TimeSpan.FromSeconds(30) // 增加超时时间，因为AI调用可能需要更长时间
        };

        // 在构造函数中测试API连接
        TestAPIConnection();
    }

    private async void TestAPIConnection()
    {
        try
        {
            // 异步测试，不阻塞构造函数
            await Task.Run(async () =>
            {
                var deepseekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
                var deepseekBaseUrl = Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL");

                if (!string.IsNullOrWhiteSpace(deepseekApiKey) && !string.IsNullOrWhiteSpace(deepseekBaseUrl))
                {
                    Console.WriteLine("正在测试 DeepSeek API 连接...");
                    var testResult = await TestDeepSeekConnection(deepseekApiKey, deepseekBaseUrl);
                    Console.WriteLine(testResult ? "✅ DeepSeek API 连接成功" : "❌ DeepSeek API 连接失败");
                }
                else
                {
                    Console.WriteLine("未配置 DeepSeek API，请设置 DEEPSEEK_API_KEY 和 DEEPSEEK_BASE_URL 环境变量");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"API连接测试失败: {ex.Message}");
        }
    }

    private async Task<bool> TestDeepSeekConnection(string apiKey, string baseUrl)
    {
        try
        {
            var endpoint = $"{baseUrl}/chat/completions";
            _httpClient.DefaultRequestHeaders.Clear(); // 清除之前的headers
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var testRequest = new
            {
                model = "deepseek-chat",
                messages = new[] { new { role = "user", content = "Hello, test connection." } },
                max_tokens = 10,
                temperature = 0.8
            };

            var content = new StringContent(JsonSerializer.Serialize(testRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);

            Console.WriteLine($"DeepSeek API测试响应状态码: {(int)response.StatusCode} {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"DeepSeek API测试错误内容: {errorContent}");
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeepSeek API测试异常: {ex.Message}");
            return false;
        }
    }


    public async Task<string> CreateNarrativeAsync(
        DailySnapshot today,
        IReadOnlyList<DailySnapshot> historicalSnapshots,
        CancellationToken cancellationToken = default)
    {
        // 首先尝试调用AI API
        var aiResponse = await TryCallAIAsync(today, historicalSnapshots, cancellationToken);
        if (!string.IsNullOrWhiteSpace(aiResponse))
        {
            return aiResponse;
        }

        // 如果AI调用失败，抛出异常而不是使用本地分析
        throw new Exception("AI服务暂时不可用，无法生成健康建议。请检查网络连接或稍后重试。");
    }

    private async Task<string> TryCallAIAsync(DailySnapshot today, IReadOnlyList<DailySnapshot> historicalSnapshots, CancellationToken cancellationToken)
    {
        try
        {
            // 首先尝试从环境变量获取
            var deepseekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            var deepseekBaseUrl = Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL");

            // 如果环境变量未设置，使用硬编码的默认值
            if (string.IsNullOrWhiteSpace(deepseekApiKey))
            {
                deepseekApiKey = "sk-edb4ae50b8044f099e56ce138d88579c";
            }

            if (string.IsNullOrWhiteSpace(deepseekBaseUrl))
            {
                deepseekBaseUrl = "https://api.deepseek.com";
            }

            if (!string.IsNullOrWhiteSpace(deepseekApiKey) && !string.IsNullOrWhiteSpace(deepseekBaseUrl))
            {
                return await CallDeepSeekAsync(today, historicalSnapshots, deepseekApiKey, deepseekBaseUrl, cancellationToken);
            }

            return null;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private async Task<string> CallDeepSeekAsync(DailySnapshot today, IReadOnlyList<DailySnapshot> historicalSnapshots, string apiKey, string baseUrl, CancellationToken cancellationToken)
    {
        var endpoint = $"{baseUrl}/chat/completions";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var prompt = BuildHealthPrompt(today, historicalSnapshots);

        var requestBody = new
        {
            model = "deepseek-chat",
            messages = new[]
            {
                new { role = "system", content = "你是一位专业的健康顾问，擅长根据用户的健康数据提供个性化建议。请用中文回复，内容要专业、实用、鼓励性强。" },
                new { role = "user", content = prompt }
            },
            max_tokens = 1500,
            temperature = 0.8
        };

        var requestJson = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"DeepSeek API请求失败: {(int)response.StatusCode} {response.StatusCode}. 响应: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            return choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }

        throw new Exception("无法解析DeepSeek响应");
    }


    private string BuildHealthPrompt(DailySnapshot today, IReadOnlyList<DailySnapshot> historicalSnapshots)
    {
        var builder = new StringBuilder();
        builder.AppendLine("请根据用户最近7天的健康数据，分析用户的健康状况并提供个性化建议。");
        builder.AppendLine();
        builder.AppendLine("今日数据：");
        builder.AppendLine(BuildTodayDetail(today));
        builder.AppendLine();
        builder.AppendLine("近7日历史数据：");

        foreach (var snapshot in historicalSnapshots.OrderByDescending(s => s.Date))
        {
            builder.AppendLine($"{snapshot.Date:yyyy-MM-dd}: {BuildTodayDetail(snapshot)}");
        }

        builder.AppendLine();
        builder.AppendLine("请从以下几个方面进行分析：");
        builder.AppendLine("1. 整体健康状况评估");
        builder.AppendLine("2. 睡眠质量分析和建议");
        builder.AppendLine("3. 饮水习惯分析和建议");
        builder.AppendLine("4. 运动和久坐情况分析和建议");
        builder.AppendLine("5. 综合健康建议和生活方式指导");
        builder.AppendLine();
        builder.AppendLine("请用通俗易懂的语言，提供具体可操作的建议。");

        return builder.ToString();
    }

    private string BuildTodayDetail(DailySnapshot snapshot)
    {
        var parts = new List<string>();

        if (snapshot.Sleep is not null)
        {
            var duration = snapshot.Sleep.Duration.TotalHours;
            var quality = snapshot.Sleep.QualityScore;
            parts.Add($"睡眠: {duration:F1}小时，质量评分: {quality}/10");
        }

        if (snapshot.Hydration is not null)
        {
            var consumed = snapshot.Hydration.ConsumedMl;
            var target = snapshot.Hydration.TargetMl;
            var goalMet = snapshot.Hydration.IsGoalMet;
            parts.Add($"饮水: {consumed:F0}ml/{target:F0}ml ({(goalMet ? "已达标" : "未达标")})");
        }

        if (snapshot.Activity is not null)
        {
            var workout = snapshot.Activity.WorkoutMinutes;
            var sedentary = snapshot.Activity.SedentaryMinutes;
            parts.Add($"运动: {workout}分钟，久坐: {sedentary}分钟");
        }

        return string.Join("，", parts);
    }

    private Task<string> GenerateLocalAnalysisAsync(DailySnapshot today, IReadOnlyList<DailySnapshot> historicalSnapshots)
    {
        // Offline fallback: build a narrative locally with enhanced analysis
        var builder = new StringBuilder();
        builder.AppendLine("【今日重点】");
        builder.AppendLine(BuildTodayLine(today));
        builder.AppendLine();

        // Enhanced analysis
        var analysis = AnalyzeHealthData(today, historicalSnapshots);
        builder.AppendLine("【健康分析】");
        builder.AppendLine(analysis);
        builder.AppendLine();

        builder.AppendLine("【近七日趋势】");
        foreach (var snapshot in historicalSnapshots)
        {
            builder.AppendLine($"- {snapshot.Date:MM-dd}: {BuildTodayLine(snapshot)}");
        }
        builder.AppendLine();
        builder.AppendLine("【个性化建议】");
        builder.AppendLine(GeneratePersonalizedAdvice(today, historicalSnapshots));

        return Task.FromResult(builder.ToString());
    }


    private static string AnalyzeHealthData(DailySnapshot today, IReadOnlyList<DailySnapshot> historicalSnapshots)
    {
        var analysis = new List<string>();

        // Sleep analysis
        if (today.Sleep is not null)
        {
            var sleepHours = today.Sleep.Duration.TotalHours;
            var quality = today.Sleep.QualityScore;

            if (sleepHours < 7)
                analysis.Add("睡眠时间不足，建议增加至7-9小时");
            else if (sleepHours > 9)
                analysis.Add("睡眠时间过长，建议控制在7-9小时范围内");

            if (quality < 3)
                analysis.Add("睡眠质量较差，建议改善睡眠环境和作息习惯");
            else if (quality >= 4)
                analysis.Add("睡眠质量良好，请保持");
        }

        // Hydration analysis
        if (today.Hydration is not null)
        {
            if (!today.Hydration.IsGoalMet)
            {
                var remaining = today.Hydration.RemainingMl;
                analysis.Add($"饮水量未达标，还需补充 {remaining:F0}ml");
            }
            else
            {
                analysis.Add("饮水目标已达成，保持良好的饮水习惯");
            }
        }

        // Activity analysis
        if (today.Activity is not null)
        {
            var workout = today.Activity.WorkoutMinutes;
            var sedentary = today.Activity.SedentaryMinutes;

            if (workout < 30)
                analysis.Add("运动量不足，建议每日至少30分钟有氧运动");
            else if (workout >= 30 && workout < 60)
                analysis.Add("运动量适中，可以适当增加运动强度");
            else
                analysis.Add("运动量优秀，请继续保持");

            if (sedentary > 480) // 8 hours
                analysis.Add("久坐时间过长，每小时应起身活动5分钟");
        }

        return analysis.Count > 0 ? string.Join("；", analysis) : "今日健康数据表现良好";
    }

    private static string GeneratePersonalizedAdvice(DailySnapshot today, IReadOnlyList<DailySnapshot> historicalSnapshots)
    {
        var advice = new List<string>();

        // Sleep advice
        if (today.Sleep is null || today.Sleep.Duration.TotalHours < 7)
        {
            advice.Add("1. 睡前1小时关闭电子设备，避免蓝光影响睡眠");
            advice.Add("2. 保持规律的作息时间，每日固定入睡和起床时间");
        }

        // Hydration advice
        if (today.Hydration is null || !today.Hydration.IsGoalMet)
        {
            advice.Add("3. 将饮水分散到全天，避免晚间集中补水");
            advice.Add("4. 可以在办公桌上放置水杯作为提醒");
        }

        // Activity advice
        if (today.Activity is null || today.Activity.SedentaryMinutes > 480)
        {
            advice.Add("5. 设置定时提醒，每小时起身活动5分钟");
            advice.Add("6. 尝试站立办公或使用站立式办公桌");
        }

        if (today.Activity is null || today.Activity.WorkoutMinutes < 30)
        {
            advice.Add("7. 选择自己喜欢的运动方式，如散步、骑行或游泳");
            advice.Add("8. 可以从小运动量开始，逐渐增加强度");
        }

        advice.Add("9. 定期记录健康数据，追踪改善趋势");
        advice.Add("10. 保持积极的心态，健康生活从点滴做起");

        return string.Join("\n", advice);
    }

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
            var goalStatus = snapshot.Hydration.IsGoalMet ? "✅" : "❌";
            parts.Add($"饮水 {ratio:P0}{goalStatus}");
        }

        if (snapshot.Activity is not null)
        {
            parts.Add($"运动 {snapshot.Activity.WorkoutMinutes}min / 久坐 {snapshot.Activity.SedentaryMinutes}min");
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


