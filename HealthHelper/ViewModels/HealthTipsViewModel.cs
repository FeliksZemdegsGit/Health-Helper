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

public partial class HealthTipsViewModel : ViewModelBase
{
    private readonly IHealthInsightsService _healthInsightsService;
    private readonly INavigationService _navigationService;

    private ObservableCollection<HealthTipItem> _allTips = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showAllTips = true;
    [ObservableProperty] private string _currentFilter = "全部";

    public string AllButtonBackground => ShowAllTips ? "#4CAF50" : "#E0E0E0";
    public string AllButtonForeground => ShowAllTips ? "White" : "Black";
    public string FavoriteButtonBackground => CurrentFilter == "收藏" ? "#4CAF50" : "#E0E0E0";
    public string FavoriteButtonForeground => CurrentFilter == "收藏" ? "White" : "Black";

    public ObservableCollection<HealthTipItem> Tips { get; } = new();

    public HealthTipsViewModel(
        IHealthInsightsService healthInsightsService,
        INavigationService navigationService)
    {
        _healthInsightsService = healthInsightsService;
        _navigationService = navigationService;

        // 异步加载数据
        Task.Run(() => LoadTipsAsync());
    }

    private async Task LoadTipsAsync()
    {
        try
        {
            IsLoading = true;
            _allTips.Clear();
            Tips.Clear();

            var allTips = await _healthInsightsService.GetAllHealthTipsAsync();

            foreach (var tip in allTips)
            {
                var isFavorited = await _healthInsightsService.IsTipFavoritedAsync(tip.Id);
                var item = new HealthTipItem(tip, isFavorited);
                _allTips.Add(item);
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            // 处理错误，可以在这里添加错误提示
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Tips.Clear();

        var filteredTips = CurrentFilter switch
        {
            "收藏" => _allTips.Where(t => t.IsFavorited),
            _ => _allTips
        };

        foreach (var tip in filteredTips)
        {
            Tips.Add(tip);
        }
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync(HealthTipItem item)
    {
        if (item == null) return;

        try
        {
            await _healthInsightsService.ToggleFavoriteAsync(item.Tip.Id);
            var isFavorited = await _healthInsightsService.IsTipFavoritedAsync(item.Tip.Id);
            item.IsFavorited = isFavorited;

            // 如果当前是收藏筛选，重新应用筛选
            if (CurrentFilter == "收藏")
            {
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            // 处理错误
        }
    }

    [RelayCommand]
    private void FilterTips(string filter)
    {
        CurrentFilter = filter;
        ShowAllTips = filter == "全部";
        ApplyFilter();

        // 通知按钮样式更新
        OnPropertyChanged(nameof(AllButtonBackground));
        OnPropertyChanged(nameof(AllButtonForeground));
        OnPropertyChanged(nameof(FavoriteButtonBackground));
        OnPropertyChanged(nameof(FavoriteButtonForeground));
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }
}

public class HealthTipItem : ObservableObject
{
    private bool _isFavorited;

    public HealthTip Tip { get; }
    public bool IsFavorited
    {
        get => _isFavorited;
        set => SetProperty(ref _isFavorited, value);
    }

    public string FavoriteIcon => IsFavorited ? "❤️" : "🤍";
    public string FavoriteText => IsFavorited ? "已收藏" : "收藏";

    public HealthTipItem(HealthTip tip, bool isFavorited)
    {
        Tip = tip;
        _isFavorited = isFavorited;
    }
}
