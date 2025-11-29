using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HealthHelper.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace HealthHelper.Views;

public partial class HistoryView : UserControl
{
    private HistoryViewModel? _viewModel;

    public HistoryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Initialized += OnInitialized;

        // 手动查找HistoryItemsPanel
        HistoryItemsPanel = this.FindControl<StackPanel>("HistoryItemsPanel");
    }

    private void OnInitialized(object? sender, System.EventArgs e)
    {
        // 再次尝试查找HistoryItemsPanel（以防在构造函数中没有找到）
        if (HistoryItemsPanel == null)
        {
            HistoryItemsPanel = this.FindControl<StackPanel>("HistoryItemsPanel");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.HistoryItems.CollectionChanged -= OnHistoryItemsChanged;
            _viewModel.DataLoaded -= OnDataLoaded;
        }

        _viewModel = DataContext as HistoryViewModel;

        if (_viewModel != null)
        {
            _viewModel.HistoryItems.CollectionChanged += OnHistoryItemsChanged;
            _viewModel.DataLoaded += OnDataLoaded;

            // 立即更新一次（可能数据还没加载完）
            await Dispatcher.UIThread.InvokeAsync(UpdateHistoryItemsAsync);

            // 延迟一点时间再更新一次，以确保数据加载完成
            await Task.Delay(100);
            await Dispatcher.UIThread.InvokeAsync(UpdateHistoryItemsAsync);
        }
    }

    private async void OnHistoryItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(UpdateHistoryItemsAsync);
    }

    private async void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && _viewModel != null)
        {
            // 检查是否滚动到接近底部（距离底部100像素内）
            var threshold = 100;
            var isNearBottom = scrollViewer.Offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height - threshold;

            if (isNearBottom && !_viewModel.IsLoadingMore && _viewModel.HasHistoryData)
            {
                // 触发加载更多数据
                await _viewModel.LoadMoreAsync();
            }
        }
    }

    private async void OnDataLoaded()
    {
        // 数据加载完成，强制更新UI
        await Dispatcher.UIThread.InvokeAsync(UpdateHistoryItemsAsync);
    }

    private void UpdateHistoryItemsAsync()
    {
        if (_viewModel == null)
        {
            return;
        }

        // 如果HistoryItemsPanel还没初始化，直接返回
        if (HistoryItemsPanel == null)
        {
            return;
        }

        HistoryItemsPanel.Children.Clear();

        foreach (var item in _viewModel.HistoryItems)
        {
            var border = new Border
            {
                Padding = new Avalonia.Thickness(16),
                BorderBrush = Avalonia.Media.Brushes.LightGray,
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(250, 250, 250)),
                Margin = new Avalonia.Thickness(0, 0, 0, 8)
            };

            var button = new Button
            {
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Command = _viewModel.ViewHistoryItemCommand,
                CommandParameter = item
            };

            var textBlock = new TextBlock
            {
                Text = item.DisplayText,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 14,
                LineHeight = 20,
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(51, 51, 51)) // 深灰色，与白色背景形成对比
            };

            button.Content = textBlock;
            border.Child = button;

            HistoryItemsPanel.Children.Add(border);
        }
    }
}
