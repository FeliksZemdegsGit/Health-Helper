using System;
using HealthHelper.ViewModels;

namespace HealthHelper.Navigation;

public interface INavigationService
{
    event EventHandler? CurrentViewModelChanged;

    ViewModelBase? CurrentViewModel { get; }

    bool CanGoBack { get; }

    void Navigate(ViewModelBase viewModel, bool addToBackStack = true);

    void GoBack();
}


