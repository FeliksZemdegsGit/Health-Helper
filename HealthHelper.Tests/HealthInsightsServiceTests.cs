using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using HealthHelper.Configuration;
using HealthHelper.Models;
using HealthHelper.Services.Contracts;
using HealthHelper.Services.Implementations;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HealthHelper.Tests;

public sealed class HealthInsightsServiceTests : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly Mock<ISystemClock> _systemClockMock;
    private readonly Mock<IRecommendationClient> _recommendationClientMock;
    private readonly HealthInsightsOptions _options;
    private readonly HealthInsightsService _service;

    public HealthInsightsServiceTests()
    {
        // Create a unique test database path
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"HealthHelper_Test_{Guid.NewGuid()}.db");

        // Initialize database first
        InitializeDatabaseForTests();

        // Setup mocks
        _systemClockMock = new Mock<ISystemClock>();
        _systemClockMock.Setup(c => c.Now).Returns(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        _systemClockMock.Setup(c => c.Today).Returns(DateOnly.FromDateTime(new DateTime(2024, 1, 1)));
        _systemClockMock.Setup(c => c.IsWeekend).Returns(false);

        _recommendationClientMock = new Mock<IRecommendationClient>();
        _recommendationClientMock.Setup(c => c.CreateNarrativeAsync(It.IsAny<DailySnapshot>(), It.IsAny<IReadOnlyList<DailySnapshot>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test AI narrative");

        // Setup options
        _options = new HealthInsightsOptions { DatabasePath = _testDatabasePath };
        var optionsMock = new Mock<IOptions<HealthInsightsOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);

        // Create service instance
        _service = new HealthInsightsService(
            _systemClockMock.Object,
            _recommendationClientMock.Object,
            optionsMock.Object);
    }

    public void Dispose()
    {
        _service?.Dispose();

        // Give it a moment for connections to close
        Thread.Sleep(100);

        if (File.Exists(_testDatabasePath))
        {
            try
            {
                File.Delete(_testDatabasePath);
            }
            catch (IOException)
            {
                // Ignore if file is still in use - will be cleaned up later
            }
        }
    }

    #region SaveSnapshotAsync Tests

    [Fact]
    public async Task SaveSnapshotAsync_WithValidSnapshot_ShouldSaveSuccessfully()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(DateOnly.FromDateTime(new DateTime(2024, 1, 1)));

        // Act
        var act = () => _service.SaveSnapshotAsync(snapshot);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveSnapshotAsync_WithNullSnapshot_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.SaveSnapshotAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveSnapshotAsync_WithDuplicateDate_ShouldUpdateExistingRecord()
    {
        // Arrange
        var date = DateOnly.FromDateTime(new DateTime(2024, 1, 1));
        var snapshot1 = CreateTestSnapshot(date);
        var snapshot2 = CreateTestSnapshot(date, 2200); // Different water amount

        // Act
        await _service.SaveSnapshotAsync(snapshot1);
        await _service.SaveSnapshotAsync(snapshot2);

        // Assert
        var count = await _service.GetStoredDayCountAsync();
        count.Should().Be(1); // Should still be 1 record
    }

    #endregion

    #region GetStoredDayCountAsync Tests

    [Fact]
    public async Task GetStoredDayCountAsync_WithEmptyDatabase_ShouldReturnZero()
    {
        // Act
        var count = await _service.GetStoredDayCountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetStoredDayCountAsync_WithData_ShouldReturnCorrectCount()
    {
        // Arrange
        await _service.SaveSnapshotAsync(CreateTestSnapshot(DateOnly.FromDateTime(new DateTime(2024, 1, 1))));
        await _service.SaveSnapshotAsync(CreateTestSnapshot(DateOnly.FromDateTime(new DateTime(2024, 1, 2))));

        // Act
        var count = await _service.GetStoredDayCountAsync();

        // Assert
        count.Should().Be(2);
    }

    #endregion

    #region GenerateCombinedAdviceAsync Tests

    [Fact]
    public async Task GenerateCombinedAdviceAsync_WithValidSnapshot_ShouldReturnAdviceBundle()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(DateOnly.FromDateTime(new DateTime(2024, 1, 1)));

        // Act
        var result = await _service.GenerateCombinedAdviceAsync(snapshot);

        // Assert
        result.Should().NotBeNull();
        result.Narrative.Should().Be("Test AI narrative");
    }

    [Fact]
    public async Task GenerateCombinedAdviceAsync_WithNullSnapshot_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.GenerateCombinedAdviceAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateCombinedAdviceAsync_ShouldCallRecommendationClient()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(DateOnly.FromDateTime(new DateTime(2024, 1, 1)));

        // Act
        await _service.GenerateCombinedAdviceAsync(snapshot);

        // Assert
        _recommendationClientMock.Verify(c => c.CreateNarrativeAsync(snapshot, It.IsAny<IReadOnlyList<DailySnapshot>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetHistoricalSnapshotsAsync Tests

    [Fact]
    public async Task GetHistoricalSnapshotsAsync_WithEmptyDatabase_ShouldReturnEmptyList()
    {
        // Act
        var snapshots = await _service.GetHistoricalSnapshotsAsync();

        // Assert
        snapshots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoricalSnapshotsAsync_WithData_ShouldReturnOrderedByDateDescending()
    {
        // Arrange
        var date1 = DateOnly.FromDateTime(new DateTime(2024, 1, 1));
        var date2 = DateOnly.FromDateTime(new DateTime(2024, 1, 2));
        var date3 = DateOnly.FromDateTime(new DateTime(2024, 1, 3));

        await _service.SaveSnapshotAsync(CreateTestSnapshot(date1));
        await _service.SaveSnapshotAsync(CreateTestSnapshot(date3));
        await _service.SaveSnapshotAsync(CreateTestSnapshot(date2));

        // Act
        var snapshots = await _service.GetHistoricalSnapshotsAsync(7);

        // Assert
        snapshots.Should().HaveCount(3);
        snapshots[0].Date.Should().Be(date3);
        snapshots[1].Date.Should().Be(date2);
        snapshots[2].Date.Should().Be(date1);
    }

    [Fact]
    public async Task GetHistoricalSnapshotsAsync_WithLimit_ShouldReturnLimitedResults()
    {
        // Arrange
        for (int i = 1; i <= 10; i++)
        {
            var date = DateOnly.FromDateTime(new DateTime(2024, 1, i));
            await _service.SaveSnapshotAsync(CreateTestSnapshot(date));
        }

        // Act
        var snapshots = await _service.GetHistoricalSnapshotsAsync(5);

        // Assert
        snapshots.Should().HaveCount(5);
    }

    #endregion

    #region GetHistoricalSnapshotsPagedAsync Tests

    [Fact]
    public async Task GetHistoricalSnapshotsPagedAsync_WithValidParameters_ShouldReturnPagedResults()
    {
        // Arrange
        for (int i = 1; i <= 10; i++)
        {
            var date = DateOnly.FromDateTime(new DateTime(2024, 1, i));
            await _service.SaveSnapshotAsync(CreateTestSnapshot(date, 2000 + i * 10));
        }

        // Act
        var page1 = await _service.GetHistoricalSnapshotsPagedAsync(3, 0);
        var page2 = await _service.GetHistoricalSnapshotsPagedAsync(3, 3);

        // Assert
        page1.Should().HaveCount(3);
        page2.Should().HaveCount(3);
        // Results are ordered by date DESC, so most recent (highest date) comes first
        page1[0].Date.Should().Be(DateOnly.FromDateTime(new DateTime(2024, 1, 10)));
        page1[0].Hydration.ConsumedMl.Should().Be(2100); // 2000 + 10 * 10
    }

    [Fact]
    public async Task GetHistoricalSnapshotsPagedAsync_WithPageSizeZero_ShouldReturnEmptyList()
    {
        // Arrange
        await _service.SaveSnapshotAsync(CreateTestSnapshot(DateOnly.FromDateTime(new DateTime(2024, 1, 1))));

        // Act
        var result = await _service.GetHistoricalSnapshotsPagedAsync(0, 0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoricalSnapshotsPagedAsync_WithNegativeSkip_ShouldReturnFromBeginning()
    {
        // Arrange
        await _service.SaveSnapshotAsync(CreateTestSnapshot(DateOnly.FromDateTime(new DateTime(2024, 1, 1))));

        // Act
        var result = await _service.GetHistoricalSnapshotsPagedAsync(10, -1);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region Health Tips Tests

    [Fact]
    public async Task GetAllHealthTipsAsync_ShouldReturnAllTips()
    {
        // Act
        var tips = await _service.GetAllHealthTipsAsync();

        // Assert
        tips.Should().NotBeNull();
        tips.Should().NotBeEmpty();
        tips.All(t => t.Id > 0).Should().BeTrue();
        tips.All(t => !string.IsNullOrEmpty(t.Content)).Should().BeTrue();
    }

    [Fact]
    public async Task GetRandomHealthTipAsync_ShouldReturnValidTip()
    {
        // Act
        var tip = await _service.GetRandomHealthTipAsync();

        // Assert
        tip.Should().NotBeNull();
        tip.Id.Should().BeGreaterThan(0);
        tip.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task IsTipFavoritedAsync_WithNonFavoritedTip_ShouldReturnFalse()
    {
        // Arrange
        var tips = await _service.GetAllHealthTipsAsync();
        var tipId = tips.First().Id;

        // Act
        var isFavorited = await _service.IsTipFavoritedAsync(tipId);

        // Assert
        isFavorited.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleFavoriteAsync_ShouldToggleFavoriteStatus()
    {
        // Arrange
        var tips = await _service.GetAllHealthTipsAsync();
        var tipId = tips.First().Id;

        // Act & Assert
        var initialStatus = await _service.IsTipFavoritedAsync(tipId);
        initialStatus.Should().BeFalse();

        await _service.ToggleFavoriteAsync(tipId);
        var afterFirstToggle = await _service.IsTipFavoritedAsync(tipId);
        afterFirstToggle.Should().BeTrue();

        await _service.ToggleFavoriteAsync(tipId);
        var afterSecondToggle = await _service.IsTipFavoritedAsync(tipId);
        afterSecondToggle.Should().BeFalse();
    }

    [Fact]
    public async Task GetFavoritedTipsAsync_WithNoFavorites_ShouldReturnEmptyList()
    {
        // Act
        var favoritedTips = await _service.GetFavoritedTipsAsync();

        // Assert
        favoritedTips.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFavoritedTipsAsync_WithFavorites_ShouldReturnFavoritedTips()
    {
        // Arrange
        var tips = await _service.GetAllHealthTipsAsync();
        var tip1 = tips.First();
        var tip2 = tips.Skip(1).First();

        await _service.ToggleFavoriteAsync(tip1.Id);
        await _service.ToggleFavoriteAsync(tip2.Id);

        // Act
        var favoritedTips = await _service.GetFavoritedTipsAsync();

        // Assert
        favoritedTips.Should().HaveCount(2);
        favoritedTips.Should().Contain(t => t.Id == tip1.Id);
        favoritedTips.Should().Contain(t => t.Id == tip2.Id);
    }

    #endregion

    #region Helper Methods

    private static DailySnapshot CreateTestSnapshot(DateOnly date, double waterAmount = 2000)
    {
        var sleepLog = new SleepLog(
            new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue)),
            new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue).AddHours(8)),
            8);

        var hydrationLog = new HydrationLog(2000, waterAmount);

        var activityLog = new ActivityLog(60, 120); // 60 min exercise, 120 min sedentary

        return new DailySnapshot(date, sleepLog, hydrationLog, activityLog);
    }

    #endregion

    #region Test Helpers

    private void InitializeDatabaseForTests()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _testDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Create tables directly
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS daily_logs (
                log_date TEXT PRIMARY KEY,
                bed_time TEXT,
                wake_time TEXT,
                sleep_quality INTEGER,
                hydration_target REAL,
                hydration_consumed REAL,
                workout_minutes INTEGER,
                sedentary_minutes INTEGER,
                advice_narrative TEXT
            );

            CREATE TABLE IF NOT EXISTS health_tips (
                id INTEGER PRIMARY KEY,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                category TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS tip_favorites (
                tip_id INTEGER PRIMARY KEY
            );
            """;
        command.ExecuteNonQuery();

        // Initialize health tips
        InitializeHealthTipsForTests(connection);
    }

    private void InitializeHealthTipsForTests(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        // Check if data already exists
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(1) FROM health_tips;";
        var count = Convert.ToInt32(checkCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);

        if (count > 0)
        {
            return; // Already initialized
        }

        var healthTips = new[]
        {
            (1, "规律作息很重要", "每天保持相同的作息时间，有助于调节生物钟，提高睡眠质量。建议晚上11点前入睡，早上6-7点起床。", "sleep"),
            (2, "多喝水保持身体水分", "成人每天应饮用至少2000ml水。早上起床后先喝一杯温水，有助于唤醒肠胃，促进新陈代谢。", "hydration"),
            (3, "适量运动强身健体", "每周进行150分钟的中等强度有氧运动，如快走、骑自行车，可以有效预防心血管疾病。", "exercise"),
            (4, "早餐要吃好", "早餐应包含蛋白质、碳水化合物和健康脂肪。建议食用全谷物、鸡蛋和水果，避免高糖食品。", "nutrition")
        };

        foreach (var (id, title, content, category) in healthTips)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT INTO health_tips (id, title, content, category, created_at)
                VALUES (@id, @title, @content, @category, @created_at);
                """;
            insertCommand.Parameters.AddWithValue("@id", id);
            insertCommand.Parameters.AddWithValue("@title", title);
            insertCommand.Parameters.AddWithValue("@content", content);
            insertCommand.Parameters.AddWithValue("@category", category);
            insertCommand.Parameters.AddWithValue("@created_at", DateTimeOffset.Now.ToString("O"));
            insertCommand.ExecuteNonQuery();
        }
    }

    #endregion
}
