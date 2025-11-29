using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Models;
using HealthHelper.Services.Contracts;
using HealthHelper.ViewModels;
using HealthHelper.Navigation;
using Moq;
using Xunit;
using FluentAssertions;

namespace HealthHelper.Tests.ViewModels
{
    public class HistoryViewModelTests
    {
        private readonly Mock<IHealthInsightsService> _healthServiceMock;
        private readonly Mock<INavigationService> _navigationServiceMock;
        private readonly Mock<Func<HistoryDetailViewModel>> _historyDetailFactoryMock;
        private readonly HistoryViewModel _viewModel;

        public HistoryViewModelTests()
        {
            _healthServiceMock = new Mock<IHealthInsightsService>();
            _navigationServiceMock = new Mock<INavigationService>();
            _historyDetailFactoryMock = new Mock<Func<HistoryDetailViewModel>>();

            // 设置模拟服务返回测试数据
            var testData = new List<DailySnapshot>
            {
                new DailySnapshot(DateOnly.Parse("2025-11-01"),
                    new SleepLog(DateTimeOffset.Parse("2025-11-01T22:00:00Z"), DateTimeOffset.Parse("2025-11-02T06:00:00Z"), 8),
                    new HydrationLog(2000, 1800),
                    new ActivityLog(60, 120)),
                new DailySnapshot(DateOnly.Parse("2025-11-02"),
                    new SleepLog(DateTimeOffset.Parse("2025-11-02T22:00:00Z"), DateTimeOffset.Parse("2025-11-03T06:00:00Z"), 8),
                    new HydrationLog(2000, 1900),
                    new ActivityLog(45, 150)),
                new DailySnapshot(DateOnly.Parse("2025-11-03"),
                    new SleepLog(DateTimeOffset.Parse("2025-11-03T22:00:00Z"), DateTimeOffset.Parse("2025-11-04T06:00:00Z"), 8),
                    new HydrationLog(2000, 2000),
                    new ActivityLog(90, 90))
            };

            _healthServiceMock.Setup(s =>
                s.GetHistoricalSnapshotsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testData);

            _healthServiceMock.Setup(s =>
                s.GetHistoricalSnapshotsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testData);

            _viewModel = new HistoryViewModel(
                _healthServiceMock.Object,
                _navigationServiceMock.Object,
                _historyDetailFactoryMock.Object);
        }

        [Fact]
        public async Task LoadDataAsync_ShouldPopulateHistoryItems()
        {
            // 等待构造函数中的异步加载完成
            await Task.Delay(100);

            // 验证数据已加载
            _viewModel.HistoryItems.Should().HaveCount(3);
            _viewModel.HasHistoryData.Should().BeTrue();
            _viewModel.IsLoading.Should().BeFalse();
        }

        [Fact]
        public async Task LoadMoreAsync_ShouldLoadAdditionalData()
        {
            // 等待初始数据加载
            await Task.Delay(100);
            var initialCount = _viewModel.HistoryItems.Count;

            // 执行加载更多 (直接调用方法而不是命令)
            await _viewModel.LoadMoreAsync();

            // 验证
            _viewModel.HistoryItems.Should().HaveCountGreaterOrEqualTo(initialCount);
            _viewModel.IsLoadingMore.Should().BeFalse();
        }

        [Fact]
        public void GoBackCommand_ShouldNavigateBack()
        {
            // 执行
            _viewModel.GoBackCommand.Execute(null);

            // 验证
            _navigationServiceMock.Verify(s => s.GoBack(), Times.Once);
        }

        [Fact]
        public async Task WhenNoData_ShouldShowEmptyMessage()
        {
            // 准备 - 创建没有数据的ViewModel
            _healthServiceMock.Setup(s =>
                s.GetHistoricalSnapshotsPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailySnapshot>());

            _healthServiceMock.Setup(s =>
                s.GetHistoricalSnapshotsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DailySnapshot>());

            var emptyViewModel = new HistoryViewModel(
                _healthServiceMock.Object,
                _navigationServiceMock.Object,
                _historyDetailFactoryMock.Object);

            // 等待数据加载完成
            await Task.Delay(100);

            // 验证
            emptyViewModel.HasHistoryData.Should().BeFalse();
            emptyViewModel.StatusMessage.Should().Contain("暂无历史记录");
        }
    }
}