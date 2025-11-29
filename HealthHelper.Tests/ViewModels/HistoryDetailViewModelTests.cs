using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HealthHelper.Models;
using HealthHelper.Navigation;
using HealthHelper.Services.Contracts;
using HealthHelper.ViewModels;
using Moq;
using Xunit;

namespace HealthHelper.Tests.ViewModels;

public class HistoryDetailViewModelTests
{
    private readonly Mock<INavigationService> _navigationServiceMock;
    private readonly Mock<IRecommendationClient> _recommendationClientMock;
    private readonly Mock<IHealthInsightsService> _healthInsightsServiceMock;
    private readonly HistoryDetailViewModel _viewModel;

    public HistoryDetailViewModelTests()
    {
        _navigationServiceMock = new Mock<INavigationService>();
        _recommendationClientMock = new Mock<IRecommendationClient>();
        _healthInsightsServiceMock = new Mock<IHealthInsightsService>();

        // 设置默认的历史数据返回
        _healthInsightsServiceMock.Setup(s =>
                s.GetHistoricalSnapshotsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DailySnapshot>());

        _viewModel = new HistoryDetailViewModel(
            _navigationServiceMock.Object,
            _recommendationClientMock.Object,
            _healthInsightsServiceMock.Object);
    }

    #region LoadSnapshot Tests

    [Fact]
    public async Task LoadSnapshot_ShouldSetDateDisplay()
    {
        // Arrange
        var date = DateOnly.Parse("2024-01-15");
        var snapshot = CreateTestSnapshot(date);

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100); // 等待异步操作完成

        // Assert
        _viewModel.DateDisplay.Should().Be("记录日期：2024年1月15日");
    }

    [Fact]
    public async Task LoadSnapshot_WithSleepData_ShouldFormatSleepInfo()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(
            DateOnly.Parse("2024-01-15"),
            sleepLog: new SleepLog(
                DateTimeOffset.Parse("2024-01-15T22:00:00Z"),
                DateTimeOffset.Parse("2024-01-16T06:00:00Z"),
                8));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.SleepInfo.Should().Contain("睡眠时间：8.0 小时");
        _viewModel.SleepInfo.Should().Contain("质量评分：8/10");
    }

    [Fact]
    public async Task LoadSnapshot_WithoutSleepData_ShouldShowDefaultMessage()
    {
        // Arrange
        var snapshot = new DailySnapshot(
            DateOnly.Parse("2024-01-15"),
            null,
            new HydrationLog(2000, 1800),
            new ActivityLog(60, 120));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.SleepInfo.Should().Be("未记录");
    }

    [Fact]
    public async Task LoadSnapshot_WithHydrationData_ShouldFormatHydrationInfo()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(
            DateOnly.Parse("2024-01-15"),
            hydrationLog: new HydrationLog(2000, 1800));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.HydrationInfo.Should().Contain("目标：2000 ml");
        _viewModel.HydrationInfo.Should().Contain("已饮：1800 ml");
        _viewModel.HydrationInfo.Should().Contain("❌ 未达标");
    }

    [Fact]
    public async Task LoadSnapshot_WithHydrationGoalMet_ShouldShowSuccessStatus()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(
            DateOnly.Parse("2024-01-15"),
            hydrationLog: new HydrationLog(2000, 2000));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.HydrationInfo.Should().Contain("✅ 已达标");
    }

    [Fact]
    public async Task LoadSnapshot_WithoutHydrationData_ShouldShowDefaultMessage()
    {
        // Arrange
        var snapshot = new DailySnapshot(
            DateOnly.Parse("2024-01-15"),
            new SleepLog(DateTimeOffset.Now, DateTimeOffset.Now.AddHours(8), 8),
            null,
            new ActivityLog(60, 120));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.HydrationInfo.Should().Be("未记录");
    }

    [Fact]
    public async Task LoadSnapshot_WithActivityData_ShouldFormatActivityInfo()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(
            DateOnly.Parse("2024-01-15"),
            activityLog: new ActivityLog(60, 120));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.ActivityInfo.Should().Contain("运动时间：60 分钟");
        _viewModel.ActivityInfo.Should().Contain("久坐时间：120 分钟");
    }

    [Fact]
    public async Task LoadSnapshot_WithoutActivityData_ShouldShowDefaultMessage()
    {
        // Arrange
        var snapshot = new DailySnapshot(
            DateOnly.Parse("2024-01-15"),
            new SleepLog(DateTimeOffset.Now, DateTimeOffset.Now.AddHours(8), 8),
            new HydrationLog(2000, 1800),
            null);

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.ActivityInfo.Should().Be("未记录");
    }

    [Fact]
    public async Task LoadSnapshot_WithStoredAdvice_ShouldSetAdviceProperties()
    {
        // Arrange
        var advice = new AdviceBundle("这是存储的健康建议");
        var snapshot = CreateTestSnapshot(
            DateOnly.Parse("2024-01-15"),
            advice: advice);

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.HistoricalAdvice.Should().Be("这是存储的健康建议");
        _viewModel.AdviceButtonText.Should().Be("查看生成建议");
        _viewModel.ShowAdviceButton.Should().BeTrue();
        _viewModel.ShowAdviceText.Should().BeFalse();
        _viewModel.ShowAdviceSection.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSnapshot_WithoutStoredAdvice_ShouldSetDefaultAdviceProperties()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(DateOnly.Parse("2024-01-15"), advice: null);

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.HistoricalAdvice.Should().BeEmpty();
        _viewModel.AdviceButtonText.Should().Be("查看生成建议");
        _viewModel.ShowAdviceButton.Should().BeTrue();
        _viewModel.ShowAdviceText.Should().BeFalse();
        _viewModel.ShowAdviceSection.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSnapshot_ShouldLoadHistoricalSnapshots()
    {
        // Arrange
        var historicalData = new List<DailySnapshot>
        {
            CreateTestSnapshot(DateOnly.Parse("2024-01-10")),
            CreateTestSnapshot(DateOnly.Parse("2024-01-11")),
            CreateTestSnapshot(DateOnly.Parse("2024-01-12"))
        };

        _healthInsightsServiceMock.Setup(s =>
                s.GetHistoricalSnapshotsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(historicalData);

        var snapshot = CreateTestSnapshot(DateOnly.Parse("2024-01-15"));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _healthInsightsServiceMock.Verify(s =>
            s.GetHistoricalSnapshotsAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadSnapshot_WhenHistoricalDataLoadFails_ShouldSetErrorMessage()
    {
        // Arrange
        _healthInsightsServiceMock.Setup(s =>
                s.GetHistoricalSnapshotsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("数据库连接失败"));

        var snapshot = CreateTestSnapshot(DateOnly.Parse("2024-01-15"));

        // Act
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Assert
        _viewModel.HistoricalAdvice.Should().Contain("加载历史数据失败");
        _viewModel.CanShowAdvice.Should().BeFalse();
    }

    #endregion

    #region GenerateHistoricalAdviceAsync Tests

    [Fact]
    public async Task GenerateHistoricalAdviceCommand_WithStoredAdvice_ShouldDisplayAdvice()
    {
        // Arrange
        var advice = new AdviceBundle("这是存储的健康建议内容");
        var snapshot = CreateTestSnapshot(DateOnly.Parse("2024-01-15"), advice: advice);
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Act
        _viewModel.GenerateHistoricalAdviceCommand.Execute(null);
        await Task.Delay(100); // 等待异步命令完成

        // Assert
        _viewModel.HistoricalAdvice.Should().Be("这是存储的健康建议内容");
        _viewModel.ShowAdviceText.Should().BeTrue();
        _viewModel.ShowAdviceButton.Should().BeFalse();
        _viewModel.ShowAdviceSection.Should().BeTrue();
        _viewModel.IsLoadingAdvice.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateHistoricalAdviceCommand_WithoutStoredAdvice_ShouldShowPromptMessage()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(DateOnly.Parse("2024-01-15"), advice: null);
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Act
        _viewModel.GenerateHistoricalAdviceCommand.Execute(null);
        await Task.Delay(100); // 等待异步命令完成

        // Assert
        _viewModel.HistoricalAdvice.Should().Contain("该日期暂无AI健康建议");
        _viewModel.HistoricalAdvice.Should().Contain("请在录入界面完成数据录入后点击");
        _viewModel.ShowAdviceText.Should().BeTrue();
        _viewModel.ShowAdviceButton.Should().BeFalse();
        _viewModel.ShowAdviceSection.Should().BeTrue();
        _viewModel.IsLoadingAdvice.Should().BeFalse();
    }

    [Fact]
    public void GenerateHistoricalAdviceCommand_WhenCurrentSnapshotIsNull_ShouldNotThrow()
    {
        // Arrange - 不调用 LoadSnapshot，保持 _currentSnapshot 为 null

        // Act
        var act = () =>
        {
            _viewModel.GenerateHistoricalAdviceCommand.Execute(null);
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GenerateHistoricalAdviceCommand_ShouldSetLoadingState()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(DateOnly.Parse("2024-01-15"));
        _viewModel.LoadSnapshot(snapshot);
        await Task.Delay(100);

        // Act
        _viewModel.GenerateHistoricalAdviceCommand.Execute(null);
        await Task.Delay(100); // 等待异步命令完成

        // Assert - 验证 IsLoadingAdvice 在 finally 中被设置为 false
        _viewModel.IsLoadingAdvice.Should().BeFalse();
    }

    #endregion

    #region ToggleAdviceSection Tests

    [Fact]
    public void ToggleAdviceSection_ShouldHideAdviceSection()
    {
        // Arrange
        _viewModel.ShowAdviceSection = true;
        _viewModel.ShowAdviceButton = false;
        _viewModel.ShowAdviceText = true;

        // Act
        _viewModel.ToggleAdviceSectionCommand.Execute(null);

        // Assert
        _viewModel.ShowAdviceSection.Should().BeFalse();
        _viewModel.ShowAdviceButton.Should().BeTrue();
        _viewModel.ShowAdviceText.Should().BeFalse();
    }

    [Fact]
    public void ToggleAdviceSection_WhenSectionIsHidden_ShouldNotBeExecutable()
    {
        // Arrange
        _viewModel.ShowAdviceSection = false;

        // Act & Assert
        _viewModel.ToggleAdviceSectionCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ToggleAdviceSection_WhenSectionIsShown_ShouldBeExecutable()
    {
        // Arrange
        _viewModel.ShowAdviceSection = true;

        // Act & Assert
        _viewModel.ToggleAdviceSectionCommand.CanExecute(null).Should().BeTrue();
    }

    #endregion

    #region GoBack Tests

    [Fact]
    public void GoBackCommand_ShouldNavigateBack()
    {
        // Act
        _viewModel.GoBackCommand.Execute(null);

        // Assert
        _navigationServiceMock.Verify(s => s.GoBack(), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static DailySnapshot CreateTestSnapshot(
        DateOnly date,
        SleepLog? sleepLog = null,
        HydrationLog? hydrationLog = null,
        ActivityLog? activityLog = null,
        AdviceBundle? advice = null)
    {
        return new DailySnapshot(
            date,
            sleepLog ?? new SleepLog(
                DateTimeOffset.Parse($"{date:yyyy-MM-dd}T22:00:00Z"),
                DateTimeOffset.Parse($"{date.AddDays(1):yyyy-MM-dd}T06:00:00Z"),
                8),
            hydrationLog ?? new HydrationLog(2000, 1800),
            activityLog ?? new ActivityLog(60, 120),
            advice);
    }

    #endregion
}

