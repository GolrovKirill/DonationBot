using Data;
using Data.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;

namespace Services.Tests;

[TestFixture]
public class GoalServiceTests
{
    private Mock<IDapperRepository> _repositoryMock;
    private Mock<ILogger<GoalService>> _loggerMock;
    private GoalService _goalService;

    [SetUp]
    public void Setup()
    {
        _repositoryMock = new Mock<IDapperRepository>();
        _loggerMock = new Mock<ILogger<GoalService>>();
        _goalService = new GoalService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task IsUserAdminAsync_AdminUser_ReturnsTrue()
    {
        // Arrange
        var telegramId = 123L;
        var adminUser = new Users { Id = 1, TelegramId = telegramId, Admin = true };

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync(adminUser);

        // Act
        var result = await _goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.True);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User") && v.ToString().Contains("admin status: True")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task IsUserAdminAsync_NonAdminUser_ReturnsFalse()
    {
        // Arrange
        var telegramId = 123L;
        var regularUser = new Users { Id = 1, TelegramId = telegramId, Admin = false };

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync(regularUser);

        // Act
        var result = await _goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsUserAdminAsync_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var telegramId = 999L;

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync((Users)null);

        // Act
        var result = await _goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task IsUserAdminAsync_RepositoryThrows_ReturnsFalseAndLogsError()
    {
        // Arrange
        var telegramId = 123L;
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ThrowsAsync(exception);

        // Act
        var result = await _goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.False);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error checking admin status")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetActiveGoalAsync_GoalExists_ReturnsGoal()
    {
        // Arrange
        var goal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 10000, CurrentAmount = 5000 };

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        // Act
        var result = await _goalService.GetActiveGoalAsync();

        // Assert
        Assert.That(result, Is.EqualTo(goal));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Active goal retrieval succeeded")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetActiveGoalAsync_NoGoal_ReturnsNull()
    {
        // Arrange
        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null);

        // Act
        var result = await _goalService.GetActiveGoalAsync();

        // Assert
        Assert.That(result, Is.Null);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Active goal retrieval failed - no active goal")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetActiveGoalAsync_RepositoryThrows_ReturnsNullAndLogsError()
    {
        // Arrange
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(exception);

        // Act
        var result = await _goalService.GetActiveGoalAsync();

        // Assert
        Assert.That(result, Is.Null);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving active goal")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetGoalStatsAsync_WithActiveGoal_ReturnsFormattedStats()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            Description = "Test Description",
            TargetAmount = 10000,
            CurrentAmount = 5000,
            CreatedAt = new DateTime(2024, 1, 1)
        };

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(25);

        _repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(50);

        // Act
        var result = await _goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Contains.Substring("Test Goal"));
        Assert.That(result, Contains.Substring("Test Description"));
        Assert.That(result, Contains.Substring("10\u00A0000₽"));
        Assert.That(result, Contains.Substring("5\u00A0000₽"));
        Assert.That(result, Contains.Substring("50,0%"));
        Assert.That(result, Contains.Substring("25"));
        Assert.That(result, Contains.Substring("50"));
        Assert.That(result, Contains.Substring("01.01.2024"));
        Assert.That(result, Contains.Substring("[■■■■■□□□□□]")); // 50% progress bar

        _repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _repositoryMock.Verify(x => x.GetCountUsersForActiveGoals(), Times.Once);
        _repositoryMock.Verify(x => x.GetCountDonationsForActiveGoals(), Times.Once);
    }

    [Test]
    public async Task GetGoalStatsAsync_NoActiveGoal_ReturnsNoActiveGoalMessage()
    {
        // Arrange
        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null);

        // Act
        var result = await _goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No active goal found for statistics")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetGoalStatsAsync_ZeroTargetAmount_HandlesCorrectly()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            TargetAmount = 0,
            CurrentAmount = 5000
        };

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(10);

        _repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(20);

        // Act
        var result = await _goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        var today = DateTime.Now.ToString("dd.MM.yyyy");
        Assert.That(result, Contains.Substring(
            $"🎯 **Test Goal** — 0₽ \n" +
            $"📝 Описание:  \n\n" +
            $"📈 Количество пожертвований на текущую цель: 20\n" +
            $"🧮 Количество пожертвовавших: 10 \n" +
            $"⏳ Дата открытия сбора: {today}\n\n" +
            $"Собрано: 5\u00A0000₽ (0,0%) \n" +
            $"[□□□□□□□□□□]"));
    }

    [Test]
    public async Task GetGoalStatsAsync_FullProgress_ShowsFullProgressBar()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            TargetAmount = 1000,
            CurrentAmount = 1000
        };

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(10);

        _repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(20);

        // Act
        var result = await _goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Contains.Substring("[■■■■■■■■■■]")); // 100% progress bar
        Assert.That(result, Contains.Substring("100,0%"));
    }

    [Test]
    public async Task GetGoalStatsAsync_RepositoryThrows_ReturnsErrorMessage()
    {
        // Arrange
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(exception);

        // Act
        var result = await _goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving active goal")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetStartStats_WithActiveGoal_ReturnsFormattedStats()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            Description = "Test Description",
            TargetAmount = 10000,
            CurrentAmount = 7500
        };

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        // Act
        var result = await _goalService.GetStartStats();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Contains.Substring("Test Goal"));
        Assert.That(result, Contains.Substring("Test Description"));
        Assert.That(result, Contains.Substring("10\u00A0000₽"));
        Assert.That(result, Contains.Substring("7\u00A0500₽"));
        Assert.That(result, Contains.Substring("75,0%"));
        Assert.That(result, Contains.Substring("[■■■■■■■■□□]")); // 75% progress bar (rounded to 8 blocks out of 10)
    }

    [Test]
    public async Task GetStartStats_NoActiveGoal_ReturnsNoActiveGoalMessage()
    {
        // Arrange
        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null);

        // Act
        var result = await _goalService.GetStartStats();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No active goal found for start statistics")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetStartStats_RepositoryThrows_ReturnsErrorMessage()
    {
        // Arrange
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(exception);

        // Act
        var result = await _goalService.GetStartStats();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving active goal")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task CreateGoalAsync_ValidGoal_CreatesAndReturnsGoal()
    {
        // Arrange
        var title = "New Goal";
        var description = "New Description";
        var targetAmount = 5000m;
        var createdGoal = new DonationGoal { Id = 1, Title = title, Description = description, TargetAmount = targetAmount };

        _repositoryMock
            .Setup(x => x.CreateGoalAsync(It.Is<DonationGoal>(g =>
                g.Title == title &&
                g.Description == description &&
                g.TargetAmount == targetAmount &&
                g.IsActive == true)))
            .ReturnsAsync(createdGoal);

        // Act
        var result = await _goalService.CreateGoalAsync(title, description, targetAmount);

        // Assert
        Assert.That(result, Is.EqualTo(createdGoal));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Goal created successfully") && v.ToString().Contains(title)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void CreateGoalAsync_RepositoryThrows_LogsErrorAndThrows()
    {
        // Arrange
        var title = "New Goal";
        var description = "New Description";
        var targetAmount = 5000m;
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.CreateGoalAsync(It.IsAny<DonationGoal>()))
            .ThrowsAsync(exception);

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() =>
            _goalService.CreateGoalAsync(title, description, targetAmount));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error creating goal") && v.ToString().Contains(title)),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void CreateProgressBar_VariousPercentages_ReturnsCorrectBars()
    {
        // Arrange
        var service = new GoalService(_repositoryMock.Object, _loggerMock.Object);

        // Use reflection to test private method
        var method = typeof(GoalService).GetMethod("CreateProgressBar",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var result0 = method?.Invoke(service, new object[] { 0.0 }) as string;
        var result25 = method?.Invoke(service, new object[] { 25.0 }) as string;
        var result50 = method?.Invoke(service, new object[] { 50.0 }) as string;
        var result75 = method?.Invoke(service, new object[] { 75.0 }) as string;
        var result99 = method?.Invoke(service, new object[] { 99.0 }) as string;
        var result100 = method?.Invoke(service, new object[] { 100.0 }) as string;
        var result110 = method?.Invoke(service, new object[] { 110.0 }) as string;

        Assert.That(result0, Is.EqualTo("[□□□□□□□□□□]"));
        Assert.That(result25, Is.EqualTo("[■■□□□□□□□□]"));
        Assert.That(result50, Is.EqualTo("[■■■■■□□□□□]"));
        Assert.That(result75, Is.EqualTo("[■■■■■■■■□□]"));
        Assert.That(result99, Is.EqualTo("[■■■■■■■■■■]")); // 99% rounds up to full bar
        Assert.That(result100, Is.EqualTo("[■■■■■■■■■■]"));
        Assert.That(result110, Is.EqualTo("[■■■■■■■■■■]")); // Over 100% still shows full bar
    }

    [Test]
    public async Task GetGoalStatsAsync_PartialProgressBar_RoundsCorrectly()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            TargetAmount = 1000,
            CurrentAmount = 123 // 12.3%
        };

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(5);

        _repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(10);

        // Act
        var result = await _goalService.GetGoalStatsAsync();

        var today = DateTime.Now.ToString("dd.MM.yyyy");
        Assert.That(result, Contains.Substring(
            $"🎯 **Test Goal** — 1 000₽ \n" +
            $"📝 Описание:  \n\n" +
            $"📈 Количество пожертвований на текущую цель: 10\n" +
            $"🧮 Количество пожертвовавших: 5 \n" +
            $"⏳ Дата открытия сбора: {today}\n\n" +
            $"Собрано: 123₽ (12,3%) \n" +
            $"[■□□□□□□□□□]"));
    }
}