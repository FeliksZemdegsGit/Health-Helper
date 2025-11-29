using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Models;
using HealthHelper.Navigation;
using HealthHelper.Services.Contracts;

namespace HealthHelper.ViewModels;

public partial class InputViewModel : ViewModelBase
{
    private readonly IHealthInsightsService _healthInsightsService;
    private readonly INavigationService _navigationService;
    private readonly Func<AdviceViewModel> _adviceViewModelFactory;
    private readonly Func<HistoryViewModel> _historyViewModelFactory;
    private readonly Func<WelcomeViewModel> _welcomeViewModelFactory;

    private SleepLog? _sleepLog;
    private HydrationLog? _hydrationLog;
    private ActivityLog? _activityLog;

    [ObservableProperty] private string _bedTimeInput = string.Empty;
    [ObservableProperty] private string _wakeTimeInput = string.Empty;
    [ObservableProperty] private string _sleepQualityInput = string.Empty;
    [ObservableProperty] private string _hydrationConsumedInput = string.Empty;
    [ObservableProperty] private string _workoutMinutesInput = string.Empty;
    [ObservableProperty] private string _sedentaryMinutesInput = string.Empty;
    [ObservableProperty] private string _statusMessage = "请录入今日健康数据";
    private const double HydrationTargetMl = 2000;

    public string HydrationTargetDisplay => $"{HydrationTargetMl:F0} ml";

    [ObservableProperty] private string _sleepStatusMessage = "尚未记录睡眠";
    [ObservableProperty] private string _hydrationStatusMessage = "尚未记录饮水";
    [ObservableProperty] private string _activityStatusMessage = "尚未记录运动";
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private bool _canGenerateAdvice = true;

    public InputViewModel(
        IHealthInsightsService healthInsightsService,
        INavigationService navigationService,
        Func<AdviceViewModel> adviceViewModelFactory,
        Func<HistoryViewModel> historyViewModelFactory,
        Func<WelcomeViewModel> welcomeViewModelFactory)
    {
        _healthInsightsService = healthInsightsService;
        _navigationService = navigationService;
        _adviceViewModelFactory = adviceViewModelFactory;
        _historyViewModelFactory = historyViewModelFactory;
        _welcomeViewModelFactory = welcomeViewModelFactory;
    }

    [RelayCommand]
    private void RecordSleep()
    {
        if (!TryParseTimePair(BedTimeInput, WakeTimeInput, out var bedTime, out var wakeTime))
        {
            StatusMessage = "睡眠时间格式错误，例如 23:15 / 07:00";
            return;
        }

        if (!int.TryParse(SleepQualityInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var quality) || quality is < 1 or > 10)
        {
            StatusMessage = "睡眠质量请输入 1-10";
            return;
        }

        _sleepLog = new SleepLog(bedTime, wakeTime, quality);
        SleepStatusMessage = $"记录成功：{_sleepLog.Duration.TotalHours:F1} 小时，质量 {quality}/10";
        StatusMessage = "睡眠记录已更新";
    }

    [RelayCommand]
    private void RecordHydration()
    {
        if (!TryParseDouble(HydrationConsumedInput, out var consumed) || consumed < 0)
        {
            StatusMessage = "已饮水量请输入非负数字 (ml)";
            return;
        }

        _hydrationLog = new HydrationLog(HydrationTargetMl, consumed);
        var goalStatus = consumed >= HydrationTargetMl ? "✅ 已达标" : "❌ 未达标";
        HydrationStatusMessage = $"目标 {HydrationTargetMl:F0} ml / 已饮 {consumed:F0} ml {goalStatus}";
        StatusMessage = "饮水记录已更新";
    }

    [RelayCommand]
    private void RecordActivity()
    {
        if (!int.TryParse(WorkoutMinutesInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var workout) || workout < 0)
        {
            StatusMessage = "运动时长请输入非负整数 (分钟)";
            return;
        }

        if (!int.TryParse(SedentaryMinutesInput, NumberStyles.Integer, CultureInfo.CurrentCulture, out var sedentary) || sedentary < 0)
        {
            StatusMessage = "久坐时长请输入非负整数 (分钟)";
            return;
        }

        _activityLog = new ActivityLog(workout, sedentary);
        ActivityStatusMessage = $"运动 {workout} 分钟 / 久坐 {sedentary} 分钟";
        StatusMessage = "运动记录已更新";
    }

    [RelayCommand]
    private async Task GenerateAdviceAsync()
    {
        if (_sleepLog is null || _hydrationLog is null || _activityLog is null)
        {
            StatusMessage = "请先完成睡眠、饮水与运动记录";
            return;
        }

        var snapshot = new DailySnapshot(
            DateOnly.FromDateTime(DateTime.Today),
            _sleepLog,
            _hydrationLog,
            _activityLog);

        try
        {
            IsBusy = true;

            var bundle = await _healthInsightsService.GenerateCombinedAdviceAsync(snapshot);

            // 将AI建议添加到snapshot中，并保存到数据库
            var snapshotWithAdvice = snapshot with { Advice = bundle };
            await _healthInsightsService.SaveSnapshotAsync(snapshotWithAdvice);

            var adviceViewModel = _adviceViewModelFactory();
            adviceViewModel.Load(snapshotWithAdvice, bundle);

            _navigationService.Navigate(adviceViewModel);
        }
        catch (Exception ex)
        {
            StatusMessage = $"生成建议失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ViewHistory()
    {
        var historyViewModel = _historyViewModelFactory();
        _navigationService.Navigate(historyViewModel);
    }

    [RelayCommand]
    private void GoBack()
    {
        var welcomeViewModel = _welcomeViewModelFactory();
        _navigationService.Navigate(welcomeViewModel, addToBackStack: false);
    }

    private static bool TryParseTimePair(string bedInput, string wakeInput, out DateTimeOffset bedTime, out DateTimeOffset wakeTime)
    {
        bedTime = default;
        wakeTime = default;

        if (!TryParseTodayTime(bedInput, out bedTime) || !TryParseTodayTime(wakeInput, out wakeTime))
        {
            return false;
        }

        if (wakeTime <= bedTime)
        {
            wakeTime = wakeTime.AddDays(1);
        }

        return true;
    }

    private static bool TryParseTodayTime(string input, out DateTimeOffset result)
    {
        result = default;
        var providers = new[] { CultureInfo.InvariantCulture, CultureInfo.CurrentCulture };

        foreach (var provider in providers)
        {
            if (TimeSpan.TryParse(input, provider, out var time))
            {
                var today = DateTime.Today;
                var local = today.Add(time);
                result = new DateTimeOffset(local);
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDouble(string input, out double value)
    {
        var providers = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };
        foreach (var provider in providers)
        {
            if (double.TryParse(input, NumberStyles.Float, provider, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    partial void OnIsBusyChanged(bool value)
    {
        CanGenerateAdvice = !value;
    }
}


