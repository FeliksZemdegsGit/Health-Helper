using System;
using HealthHelper.Navigation;

namespace HealthHelper.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ViewModelBase? CurrentViewModel => _navigationService.CurrentViewModel;

    public bool CanGoBack => _navigationService.CanGoBack;

    public MainWindowViewModel(INavigationService navigationService, WelcomeViewModel welcomeViewModel)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewModelChanged += HandleNavigationChanged;

        _navigationService.Navigate(welcomeViewModel, addToBackStack: false);
        OnPropertyChanged(nameof(CurrentViewModel));
        OnPropertyChanged(nameof(CanGoBack));
    }

    private void HandleNavigationChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentViewModel));
        OnPropertyChanged(nameof(CanGoBack));
    }
}