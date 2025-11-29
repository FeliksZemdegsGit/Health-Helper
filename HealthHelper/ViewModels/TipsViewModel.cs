using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Models;
using HealthHelper.Navigation;
using HealthHelper.Services.Contracts;

namespace HealthHelper.ViewModels;

public partial class TipsViewModel : ViewModelBase
{
    private readonly IHealthInsightsService _healthInsightsService;
    private readonly INavigationService _navigationService;
    private readonly Func<HealthTipsViewModel> _healthTipsViewModelFactory;

    [ObservableProperty] private HealthTip _currentTip = null!;
    [ObservableProperty] private bool _isLoading;

    public TipsViewModel(
        IHealthInsightsService healthInsightsService,
        INavigationService navigationService,
        Func<HealthTipsViewModel> healthTipsViewModelFactory)
    {
        _healthInsightsService = healthInsightsService;
        _navigationService = navigationService;
        _healthTipsViewModelFactory = healthTipsViewModelFactory;

        // 异步加载小贴士
        Task.Run(() => LoadRandomTipAsync());
    }

    private async Task LoadRandomTipAsync()
    {
        try
        {
            IsLoading = true;
            var tip = await _healthInsightsService.GetRandomHealthTipAsync();
            CurrentTip = tip;
        }
        catch (Exception ex)
        {
            // 如果加载失败，使用默认小贴士
            CurrentTip = new HealthTip(
                1,
                "保持健康的生活方式",
                "规律作息、多喝水、适量运动是保持身体健康的基础。",
                "general",
                DateTimeOffset.Now);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ViewAllTips()
    {
        var healthTipsViewModel = _healthTipsViewModelFactory();
        _navigationService.Navigate(healthTipsViewModel);
    }
}
