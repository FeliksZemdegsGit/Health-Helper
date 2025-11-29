using Avalonia.Controls;
using Avalonia.Interactivity;
using HealthHelper.ViewModels;

namespace HealthHelper.Views;

public partial class HealthTipsView : UserControl
{
    public HealthTipsView()
    {
        InitializeComponent();
    }

    private async void FavoriteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HealthTipItem item && DataContext is HealthTipsViewModel viewModel)
        {
            await viewModel.ToggleFavoriteAsync(item);
        }
    }
}
