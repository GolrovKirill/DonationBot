// <copyright file="GoalServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Data;
using Data.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;

namespace Services.Tests;

/// <summary>
/// Contains unit tests for the <see cref="GoalService"/> class.
/// Tests user authorization, goal management, statistics generation, and progress tracking functionality.
/// </summary>
[TestFixture]
public class GoalServiceTests
{
    private Mock<IDapperRepository> repositoryMock;
    private Mock<ILogger<GoalService>> loggerMock;
    private GoalService goalService;

    /// <summary>
    /// Initializes test environment before each test execution.
    /// Sets up mock dependencies, creates service instance, and configures Russian culture for formatting tests.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        repositoryMock = new Mock<IDapperRepository>();
        loggerMock = new Mock<ILogger<GoalService>>();
        goalService = new GoalService(repositoryMock.Object, loggerMock.Object);

        Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("ru-RU");
    }

    /// <summary>
    /// Tests that a user with admin privileges is correctly identified as an administrator.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IsUserAdminAsyncAdminUserReturnsTrue()
    {
        // Arrange
        var telegramId = 123L;
        var adminUser = new Users { Id = 1, TelegramId = telegramId, Admin = true };

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync(adminUser);

        // Act
        var result = await goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.True);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("User") && v.ToString() !.Contains("admin status: True")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a non-admin user is correctly identified as not having administrator privileges.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IsUserAdminAsyncNonAdminUserReturnsFalse()
    {
        // Arrange
        var telegramId = 123L;
        var regularUser = new Users { Id = 1, TelegramId = telegramId, Admin = false };

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync(regularUser);

        // Act
        var result = await goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests that when a user is not found in the database, the method returns false.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IsUserAdminAsyncUserNotFoundReturnsFalse()
    {
        // Arrange
        var telegramId = 999L;

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync((Users)null!);

        // Act
        var result = await goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests that when the repository throws an exception, the method returns false and logs an error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IsUserAdminAsyncRepositoryThrowsReturnsFalseAndLogsError()
    {
        // Arrange
        var telegramId = 123L;
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ThrowsAsync(exception);

        // Act
        var result = await goalService.IsUserAdminAsync(telegramId);

        // Assert
        Assert.That(result, Is.False);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error checking admin status")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when an active goal exists, it is successfully retrieved from the repository.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetActiveGoalAsyncGoalExistsReturnsGoal()
    {
        // Arrange
        var goal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 10000, CurrentAmount = 5000 };

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        // Act
        var result = await goalService.GetActiveGoalAsync();

        // Assert
        Assert.That(result, Is.EqualTo(goal));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Active goal retrieval succeeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when no active goal exists, the method returns null.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetActiveGoalAsyncNoGoalReturnsNull()
    {
        // Arrange
        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null!);

        // Act
        var result = await goalService.GetActiveGoalAsync();

        // Assert
        Assert.That(result, Is.Null);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Active goal retrieval failed - no active goal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when the repository throws an exception, the method returns null and logs an error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetActiveGoalAsyncRepositoryThrowsReturnsNullAndLogsError()
    {
        // Arrange
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(exception);

        // Act
        var result = await goalService.GetActiveGoalAsync();

        // Assert
        Assert.That(result, Is.Null);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error retrieving active goal")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when an active goal exists, detailed statistics are correctly formatted and returned.
    /// Includes verification of progress bars, percentage calculations, and formatted currency values.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetGoalStatsAsyncWithActiveGoalReturnsFormattedStats()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            Description = "Test Description",
            TargetAmount = 10000,
            CurrentAmount = 5000,
            CreatedAt = new DateTime(2024, 1, 1),
        };

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(25);

        repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(50);

        // Act
        var result = await goalService.GetGoalStatsAsync();

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

        repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        repositoryMock.Verify(x => x.GetCountUsersForActiveGoals(), Times.Once);
        repositoryMock.Verify(x => x.GetCountDonationsForActiveGoals(), Times.Once);
    }

    /// <summary>
    /// Tests that when no active goal exists, an appropriate message is returned to the user.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetGoalStatsAsyncNoActiveGoalReturnsNoActiveGoalMessage()
    {
        // Arrange
        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null!);

        // Act
        var result = await goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No active goal found for statistics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that goal statistics handle zero target amounts correctly to prevent division by zero.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetGoalStatsAsyncZeroTargetAmountHandlesCorrectly()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            TargetAmount = 0,
            CurrentAmount = 5000,
        };

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(10);

        repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(20);

        // Act
        var result = await goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        var today = DateTime.UtcNow.ToString("dd.MM.yyyy");
        Assert.That(result, Contains.Substring(
            $"🎯 **Test Goal** — 0₽ \n" +
            $"📝 Описание:  \n\n" +
            $"📈 Количество пожертвований на текущую цель: 20\n" +
            $"🧮 Количество пожертвовавших: 10 \n" +
            $"⏳ Дата открытия сбора: {today}\n\n" +
            $"Собрано: 5\u00A0000₽ (0,0%) \n" +
            $"[□□□□□□□□□□]"));
    }

    /// <summary>
    /// Tests that when goal progress reaches 100%, a full progress bar is displayed.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetGoalStatsAsyncFullProgressShowsFullProgressBar()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            TargetAmount = 1000,
            CurrentAmount = 1000,
        };

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(10);

        repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(20);

        // Act
        var result = await goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Contains.Substring("[■■■■■■■■■■]")); // 100% progress bar
        Assert.That(result, Contains.Substring("100,0%"));
    }

    /// <summary>
    /// Tests that when repository operations fail, an error message is returned and the exception is logged.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetGoalStatsAsyncRepositoryThrowsReturnsErrorMessage()
    {
        // Arrange
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(exception);

        // Act
        var result = await goalService.GetGoalStatsAsync();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error retrieving active goal")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that start command statistics are correctly formatted when an active goal exists.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetStartStatsWithActiveGoalReturnsFormattedStats()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            Description = "Test Description",
            TargetAmount = 10000,
            CurrentAmount = 7500,
        };

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        // Act
        var result = await goalService.GetStartStats();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Contains.Substring("Test Goal"));
        Assert.That(result, Contains.Substring("Test Description"));
        Assert.That(result, Contains.Substring("10\u00A0000₽"));
        Assert.That(result, Contains.Substring("7\u00A0500₽"));
        Assert.That(result, Contains.Substring("75,0%"));
        Assert.That(result, Contains.Substring("[■■■■■■■■□□]")); // 75% progress bar (rounded to 8 blocks out of 10)
    }

    /// <summary>
    /// Tests that when no active goal exists for start command, an appropriate message is returned.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetStartStatsNoActiveGoalReturnsNoActiveGoalMessage()
    {
        // Arrange
        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null!);

        // Act
        var result = await goalService.GetStartStats();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No active goal found for start statistics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that repository exceptions during start statistics retrieval are handled gracefully.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetStartStatsRepositoryThrowsReturnsErrorMessage()
    {
        // Arrange
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(exception);

        // Act
        var result = await goalService.GetStartStats();

        // Assert
        Assert.That(result, Is.EqualTo("🎯 На данный момент нет активных целей для сбора."));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error retrieving active goal")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a valid goal is successfully created and persisted to the repository.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CreateGoalAsyncValidGoalCreatesAndReturnsGoal()
    {
        // Arrange
        var title = "New Goal";
        var description = "New Description";
        var targetAmount = 5000m;
        var createdGoal = new DonationGoal { Id = 1, Title = title, Description = description, TargetAmount = targetAmount };

        repositoryMock
            .Setup(x => x.CreateGoalAsync(It.Is<DonationGoal>(g =>
                g.Title == title &&
                g.Description == description &&
                g.TargetAmount == targetAmount &&
                g.IsActive == true)))
            .ReturnsAsync(createdGoal);

        // Act
        var result = await goalService.CreateGoalAsync(title, description, targetAmount);

        // Assert
        Assert.That(result, Is.EqualTo(createdGoal));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Goal created successfully") && v.ToString() !.Contains(title)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that repository exceptions during goal creation are properly logged and re-thrown.
    /// </summary>
    [Test]
    public void CreateGoalAsyncRepositoryThrowsLogsErrorAndThrows()
    {
        // Arrange
        var title = "New Goal";
        var description = "New Description";
        var targetAmount = 5000m;
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.CreateGoalAsync(It.IsAny<DonationGoal>()))
            .ThrowsAsync(exception);

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() =>
            goalService.CreateGoalAsync(title, description, targetAmount));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error creating goal") && v.ToString() !.Contains(title)),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the progress bar generation correctly converts percentage values to visual representations.
    /// Includes edge cases like 0%, 100%, and values exceeding 100%.
    /// </summary>
    [Test]
    public void CreateProgressBarVariousPercentagesReturnsCorrectBars()
    {
        // Arrange
        var service = new GoalService(repositoryMock.Object, loggerMock.Object);

        // Use reflection to test private method
        var method = typeof(GoalService).GetMethod(
            "CreateProgressBar",
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

    /// <summary>
    /// Tests that partial progress percentages are correctly rounded for visual progress bar display.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetGoalStatsAsyncPartialProgressBarRoundsCorrectly()
    {
        // Arrange
        var goal = new DonationGoal
        {
            Id = 1,
            Title = "Test Goal",
            TargetAmount = 1000,
            CurrentAmount = 123, // 12.3%
        };

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.GetCountUsersForActiveGoals())
            .ReturnsAsync(5);

        repositoryMock
            .Setup(x => x.GetCountDonationsForActiveGoals())
            .ReturnsAsync(10);

        // Act
        var result = await goalService.GetGoalStatsAsync();

        var today = DateTime.UtcNow.ToString("dd.MM.yyyy");
        Assert.That(result, Contains.Substring(
            $"🎯 **Test Goal** — 1\u00A0000₽ \n" +
            $"📝 Описание:  \n\n" +
            $"📈 Количество пожертвований на текущую цель: 10\n" +
            $"🧮 Количество пожертвовавших: 5 \n" +
            $"⏳ Дата открытия сбора: {today}\n\n" +
            $"Собрано: 123₽ (12,3%) \n" +
            $"[■□□□□□□□□□]"));
    }
}