using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HealthHelper.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HealthHelper.Views;

public partial class InputView : UserControl
{
    private readonly IHealthInsightsService _healthInsightsService;

    public InputView() : this(App.Services.GetRequiredService<IHealthInsightsService>())
    {
    }

    public InputView(IHealthInsightsService healthInsightsService)
    {
        _healthInsightsService = healthInsightsService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (this.FindControl<TextBlock>("StorageStatusBlock") is not { } block)
        {
            return;
        }

        try
        {
            var days = await _healthInsightsService.GetStoredDayCountAsync().ConfigureAwait(true);
            block.Text = days > 0
                ? $"SQLite 已存 {days} 天历史数据"
                : "SQLite 尚无历史数据";
        }
        catch (Exception ex)
        {
            block.Text = $"读取历史数据失败：{ex.Message}";
        }
    }
}

