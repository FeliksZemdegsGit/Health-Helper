using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using HealthHelper.ViewModels;

namespace HealthHelper.Navigation;

public sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly Stack<ViewModelBase> _history = new();
    private ViewModelBase? _currentViewModel;

    public event EventHandler? CurrentViewModelChanged;

    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (SetProperty(ref _currentViewModel, value))
            {
                OnPropertyChanged(nameof(CanGoBack));
                CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool CanGoBack => _history.Count > 0;

    public void Navigate(ViewModelBase viewModel, bool addToBackStack = true)
    {
        if (addToBackStack && _currentViewModel is not null)
        {
            _history.Push(_currentViewModel);
        }

        CurrentViewModel = viewModel;
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        CurrentViewModel = _history.Pop();
    }
}

