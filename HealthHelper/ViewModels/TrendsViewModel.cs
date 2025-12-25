using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Navigation;
using HealthHelper.Services.Contracts;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace HealthHelper.ViewModels;

public partial class TrendsViewModel : ViewModelBase
{
    private readonly IHealthInsightsService _healthInsightsService;
    private readonly INavigationService _navigationService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ISeries[] SleepSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] HydrationSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] WorkoutSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] SedentarySeries { get; private set; } = Array.Empty<ISeries>();

    public Axis[] XAxes { get; private set; } =
    [
        new Axis
        {
            Labels = Array.Empty<string>(),
            LabelsRotation = 0,
            TextSize = 12
        }
    ];

    public TrendsViewModel(IHealthInsightsService healthInsightsService, INavigationService navigationService)
    {
        _healthInsightsService = healthInsightsService;
        _navigationService = navigationService;

        Task.Run(LoadAsync);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = "正在加载近7日趋势...";

        try
        {
            var snapshots = await _healthInsightsService.GetHistoricalSnapshotsAsync(7);
            var ordered = snapshots
                .OrderBy(s => s.Date)
                .ToList();

            if (ordered.Count == 0)
            {
                SleepSeries = Array.Empty<ISeries>();
                HydrationSeries = Array.Empty<ISeries>();
                WorkoutSeries = Array.Empty<ISeries>();
                SedentarySeries = Array.Empty<ISeries>();
                XAxes[0].Labels = Array.Empty<string>();

                StatusMessage = "暂无数据，请先录入健康数据。";
                RaiseChartPropertiesChanged();
                return;
            }

            var labels = ordered
                .Select(s => s.Date.ToString("MM-dd", CultureInfo.InvariantCulture))
                .ToArray();

            // 缺失数据按 0 处理（更直观）；如果你想断线，可以改成 double.NaN。
            var sleepHours = ordered.Select(s => s.Sleep?.Duration.TotalHours ?? 0d).ToArray();
            var hydrationMl = ordered.Select(s => s.Hydration?.ConsumedMl ?? 0d).ToArray();
            var workoutMin = ordered.Select(s => (double)(s.Activity?.WorkoutMinutes ?? 0)).ToArray();
            var sedentaryMin = ordered.Select(s => (double)(s.Activity?.SedentaryMinutes ?? 0)).ToArray();

            XAxes[0].Labels = labels;

            // 说明：tooltip 中文“□□□”的根因是 Skia 字体缺字。
            // 在不做全局设置的前提下，最稳妥的做法是：避免在 tooltip/legend 中输出中文。
            // 因此 Series.Name 使用英文，标题仍在 UI TextBlock 中用中文显示。
            SleepSeries = BuildLineSeries(sleepHours, "Sleep (h)");
            HydrationSeries = BuildLineSeries(hydrationMl, "Water (ml)");
            WorkoutSeries = BuildLineSeries(workoutMin, "Workout (min)");
            SedentarySeries = BuildLineSeries(sedentaryMin, "Sedentary (min)");

            StatusMessage = $"已加载近 {ordered.Count} 天趋势";
            RaiseChartPropertiesChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static ISeries[] BuildLineSeries(IReadOnlyList<double> values, string name)
    {
        return
        [
            new LineSeries<double>
            {
                Name = name,
                Values = values,
                GeometrySize = 6,
                LineSmoothness = 0.2
            }
        ];
    }

    private void RaiseChartPropertiesChanged()
    {
        OnPropertyChanged(nameof(SleepSeries));
        OnPropertyChanged(nameof(HydrationSeries));
        OnPropertyChanged(nameof(WorkoutSeries));
        OnPropertyChanged(nameof(SedentarySeries));
        OnPropertyChanged(nameof(XAxes));
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
