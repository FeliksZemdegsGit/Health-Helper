using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Models;
using HealthHelper.Navigation;
using HealthHelper.Services.Contracts;

namespace HealthHelper.ViewModels;

public partial class HistoryDetailViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IRecommendationClient _recommendationClient;
    private readonly IHealthInsightsService _healthInsightsService;

    private DailySnapshot _currentSnapshot = null!;
    private IReadOnlyList<DailySnapshot> _historicalSnapshots = Array.Empty<DailySnapshot>();
    private AdviceBundle? _storedAdvice;

    [ObservableProperty] private string _dateDisplay = string.Empty;
    [ObservableProperty] private string _sleepInfo = "未记录";
    [ObservableProperty] private string _hydrationInfo = "未记录";
    [ObservableProperty] private string _activityInfo = "未记录";
    [ObservableProperty] private string _historicalAdvice = string.Empty;
    [ObservableProperty] private bool _isLoadingAdvice;
    [ObservableProperty] private bool _canShowAdvice = true;
    [ObservableProperty] private bool _showAdviceButton;
    [ObservableProperty] private bool _showAdviceText;
    [ObservableProperty] private string _adviceButtonText = "查看生成建议";
    [ObservableProperty] private bool _showAdviceSection = false;

    public HistoryDetailViewModel(
        INavigationService navigationService,
        IRecommendationClient recommendationClient,
        IHealthInsightsService healthInsightsService)
    {
        _navigationService = navigationService;
        _recommendationClient = recommendationClient;
        _healthInsightsService = healthInsightsService;
    }

    public async void LoadSnapshot(DailySnapshot snapshot)
    {
        _currentSnapshot = snapshot;

        DateDisplay = $"记录日期：{snapshot.Date:yyyy年M月d日}";

        // 睡眠信息
        if (snapshot.Sleep is not null)
        {
            SleepInfo = $"睡眠时间：{snapshot.Sleep.Duration.TotalHours:F1} 小时 | 质量评分：{snapshot.Sleep.QualityScore}/10";
        }

        // 饮水信息
        if (snapshot.Hydration is not null)
        {
            var goalStatus = snapshot.Hydration.IsGoalMet ? "✅ 已达标" : "❌ 未达标";
            HydrationInfo = $"目标：{snapshot.Hydration.TargetMl:F0} ml | 已饮：{snapshot.Hydration.ConsumedMl:F0} ml | {goalStatus}";
        }

        // 活动信息
        if (snapshot.Activity is not null)
        {
            ActivityInfo = $"运动时间：{snapshot.Activity.WorkoutMinutes} 分钟 | 久坐时间：{snapshot.Activity.SedentaryMinutes} 分钟";
        }

        // 默认隐藏建议区域，显示查看按钮
        ShowAdviceButton = true;
        ShowAdviceText = false; // 默认收起
        ShowAdviceSection = false; // 整个建议区域隐藏

        if (snapshot.Advice is not null)
        {
            // 有存储的建议
            HistoricalAdvice = snapshot.Advice.Narrative;
            AdviceButtonText = "查看生成建议";
            _storedAdvice = snapshot.Advice;
        }
        else
        {
            // 没有存储的建议
            HistoricalAdvice = "";
            AdviceButtonText = "查看生成建议";
            _storedAdvice = null;
        }

        // 加载历史数据（用于生成建议）
        try
        {
            _historicalSnapshots = await _healthInsightsService.GetHistoricalSnapshotsAsync(7);
        }
        catch (Exception ex)
        {
            HistoricalAdvice = $"加载历史数据失败：{ex.Message}";
            CanShowAdvice = false;
        }
    }

    [RelayCommand]
    private async Task GenerateHistoricalAdviceAsync()
    {
        if (_currentSnapshot is null) return;

        IsLoadingAdvice = true;

        try
        {
            ShowAdviceSection = true; // 显示整个建议区域

            if (_storedAdvice is not null)
            {
                // 有存储的建议，直接显示
                HistoricalAdvice = _storedAdvice.Narrative;
                ShowAdviceText = true;
                ShowAdviceButton = false; // 显示建议后隐藏按钮
            }
            else
            {
                // 没有存储的建议，显示提示信息，不要调用AI
                HistoricalAdvice = "该日期暂无AI健康建议。如需生成建议，请在录入界面完成数据录入后点击\"生成健康建议\"按钮。";
                ShowAdviceText = true;
                ShowAdviceButton = false; // 显示提示后隐藏按钮
            }
        }
        catch (Exception ex)
        {
            HistoricalAdvice = $"操作失败：{ex.Message}";
        }
        finally
        {
            IsLoadingAdvice = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggleAdvice))]
    internal void ToggleAdviceSection()
    {
        // 收起建议区域：隐藏区域，显示查看按钮，隐藏文本
        ShowAdviceSection = false;
        ShowAdviceButton = true;
        ShowAdviceText = false;
    }

    private bool CanToggleAdvice => ShowAdviceSection; // 只有在展开状态下才能收起

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }
}
