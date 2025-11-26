using System;
using Avalonia.Controls;
using HealthHelper.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HealthHelper.Views;

public partial class MainWindow : Window
{
    public MainWindow() : this(ResolveViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private static MainWindowViewModel ResolveViewModel()
    {
        if (App.Services is null)
        {
            throw new InvalidOperationException("Service provider has not been configured.");
        }

        return App.Services.GetRequiredService<MainWindowViewModel>();
    }
}