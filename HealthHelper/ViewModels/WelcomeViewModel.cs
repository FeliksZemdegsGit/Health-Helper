using System;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Navigation;
using HealthHelper.Services.Contracts;

namespace HealthHelper.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly Func<InputViewModel> _inputViewModelFactory;
    private readonly Func<HistoryViewModel> _historyViewModelFactory;
    private readonly Func<HealthTipsViewModel> _healthTipsViewModelFactory;
    private readonly Func<TrendsViewModel> _trendsViewModelFactory;
    private readonly IHealthInsightsService _healthInsightsService;

    public TipsViewModel TipsViewModel { get; }

    public WelcomeViewModel(
        INavigationService navigationService,
        Func<InputViewModel> inputViewModelFactory,
        Func<HistoryViewModel> historyViewModelFactory,
        Func<HealthTipsViewModel> healthTipsViewModelFactory,
        Func<TrendsViewModel> trendsViewModelFactory,
        IHealthInsightsService healthInsightsService)
    {
        _navigationService = navigationService;
        _inputViewModelFactory = inputViewModelFactory;
        _historyViewModelFactory = historyViewModelFactory;
        _healthTipsViewModelFactory = healthTipsViewModelFactory;
        _trendsViewModelFactory = trendsViewModelFactory;
        _healthInsightsService = healthInsightsService;

        TipsViewModel = new TipsViewModel(
            healthInsightsService,
            navigationService,
            healthTipsViewModelFactory);
    }

    [RelayCommand]
    private void StartInput()
    {
        var inputViewModel = _inputViewModelFactory();
        _navigationService.Navigate(inputViewModel);
    }

    [RelayCommand]
    private void ViewHistory()
    {
        var historyViewModel = _historyViewModelFactory();
        _navigationService.Navigate(historyViewModel);
    }

    [RelayCommand]
    private void ViewTrends()
    {
        var trendsViewModel = _trendsViewModelFactory();
        _navigationService.Navigate(trendsViewModel);
    }
}
