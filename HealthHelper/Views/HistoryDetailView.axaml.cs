using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HealthHelper.Views;

public partial class HistoryDetailView : UserControl
{
    public HistoryDetailView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
