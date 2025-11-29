using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Models;
using HealthHelper.Navigation;
using HealthHelper.Services.Contracts;

namespace HealthHelper.ViewModels;

public class HistoryItem
{
    public string DisplayText { get; set; } = string.Empty;
    public DailySnapshot Snapshot { get; set; } = null!;
    public int Index { get; set; }
}

public partial class HistoryViewModel : ViewModelBase
{
    public event Action? DataLoaded;
    private readonly IHealthInsightsService _healthInsightsService;
    private readonly INavigationService _navigationService;
    private readonly Func<HistoryDetailViewModel> _historyDetailViewModelFactory;

    private const int PageSize = 10; // 每页加载10条记录
    private int _currentPage = 0;
    private bool _hasMoreData = true;
    private int _totalRecords = 0;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoadingMore; // 加载更多数据的状态
    [ObservableProperty] private string _statusMessage = "正在加载历史记录...";

    public ObservableCollection<DailySnapshot> HistoricalData { get; } = new();
    public ObservableCollection<HistoryItem> HistoryItems { get; } = new();

    public bool HasHistoryData => HistoryItems.Count > 0;

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StatusMessage = "正在加载历史记录...";

        try
        {
            // 重置分页状态
            _currentPage = 0;
            _hasMoreData = true;
            HistoricalData.Clear();
            HistoryItems.Clear();

            // 获取总记录数（这里先获取所有数据，实际项目中应该有专门的计数方法）
            var allData = await _healthInsightsService.GetHistoricalSnapshotsAsync(int.MaxValue);
            _totalRecords = allData.Count;

            // 加载第一页数据
            await LoadPageDataAsync(0);

            StatusMessage = HistoryItems.Count > 0
                ? $"已加载 {HistoryItems.Count} 条历史记录" + (_hasMoreData ? "，下拉加载更多" : "")
                : "暂无历史记录，请先录入一些健康数据";

            // 触发UI更新
            RefreshUI();

            // 触发数据加载完成事件
            DataLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
            RefreshUI();
            DataLoaded?.Invoke();
        }
        finally
        {
            IsLoading = false;
        }
    }

    // 加载更多数据的命令
    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsLoadingMore || !_hasMoreData) return;

        IsLoadingMore = true;
        StatusMessage = "正在加载更多历史记录...";

        try
        {
            await LoadPageDataAsync(_currentPage);

            StatusMessage = HistoryItems.Count > 0
                ? $"已加载 {HistoryItems.Count} 条历史记录" + (_hasMoreData ? "，下拉加载更多" : "")
                : "暂无历史记录，请先录入一些健康数据";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载更多失败: {ex.Message}";
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    // 加载指定页面的数据
    private async Task LoadPageDataAsync(int pageIndex)
    {
        var skip = pageIndex * PageSize;
        var data = await _healthInsightsService.GetHistoricalSnapshotsPagedAsync(PageSize, skip);

        if (data.Count == 0)
        {
            _hasMoreData = false;
            return;
        }

        // 添加到现有数据
        foreach (var snapshot in data)
        {
            HistoricalData.Add(snapshot);
        }

        // 创建历史记录项
        var startIndex = pageIndex * PageSize;
        for (int i = 0; i < data.Count; i++)
        {
            var snapshot = data[i];
            var displayText = $"📅 {snapshot.Date:yyyy年M月d日}\n" +
                             $"😴 睡眠: {(snapshot.Sleep != null ? $"{snapshot.Sleep.Duration.TotalHours:F1}小时" : "未记录")}\n" +
                             $"💧 饮水: {(snapshot.Hydration != null ? $"{snapshot.Hydration.ConsumedMl:F0}ml" : "未记录")}\n" +
                             $"🏃 运动: {(snapshot.Activity != null ? $"{snapshot.Activity.WorkoutMinutes}分钟" : "未记录")}\n\n" +
                             "点击查看详情和AI建议 →";

            HistoryItems.Add(new HistoryItem
            {
                DisplayText = displayText,
                Snapshot = snapshot,
                Index = startIndex + i + 1
            });
        }

        _currentPage = pageIndex + 1;
        _hasMoreData = data.Count == PageSize && HistoryItems.Count < _totalRecords;
    }

    [RelayCommand]
    private void ViewSnapshotDetail(DailySnapshot snapshot)
    {
        var detailViewModel = _historyDetailViewModelFactory();
        detailViewModel.LoadSnapshot(snapshot);
        _navigationService.Navigate(detailViewModel);
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }

    [RelayCommand]
    private void ViewHistoryItem(HistoryItem item)
    {
        if (item?.Snapshot != null)
        {
            ViewSnapshotDetail(item.Snapshot);
        }
    }

    // 公开的方法，供View调用来刷新UI
    public void RefreshUI()
    {
        // 触发属性变化通知，迫使UI重新绑定
        OnPropertyChanged(nameof(HistoryItems));
        OnPropertyChanged(nameof(HasHistoryData));

        // 直接触发集合变化事件，强制UI更新
        var temp = HistoryItems.ToList();
        HistoryItems.Clear();
        foreach (var item in temp)
        {
            HistoryItems.Add(item);
        }
    }

    // 构造函数中加载数据
    public HistoryViewModel(
        IHealthInsightsService healthInsightsService,
        INavigationService navigationService,
        Func<HistoryDetailViewModel> historyDetailViewModelFactory)
        : base()
    {
        _healthInsightsService = healthInsightsService;
        _navigationService = navigationService;
        _historyDetailViewModelFactory = historyDetailViewModelFactory;

        // 异步加载历史数据（使用Task.Run避免阻塞构造函数）
        Task.Run(() => LoadDataAsync());
    }
}
