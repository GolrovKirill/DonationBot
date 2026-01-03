// <copyright file="AdminHandlerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bot.Handlers;
using Bot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using static Bot.Services.AdminStateService;

namespace Bot.Tests.Handlers;

/// <summary>
/// Unit tests for the <see cref="AdminHandler"/> class.
/// </summary>
[TestFixture]
public class AdminHandlerTests
{
    private Mock<ILogger<AdminHandler>> loggerMock;
    private Mock<IGoalService> goalServiceMock;
    private Mock<AdminStateService> adminStateServiceMock;
    private Mock<ITelegramBotClient> botClientMock;
    private AdminHandler handler;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        loggerMock = new Mock<ILogger<AdminHandler>>();
        goalServiceMock = new Mock<IGoalService>();

        // Создаем мок для AdminStateService с правильным конструктором
        var adminStateServiceLoggerMock = new Mock<ILogger<AdminStateService>>();
        adminStateServiceMock = new Mock<AdminStateService>(adminStateServiceLoggerMock.Object);

        botClientMock = new Mock<ITelegramBotClient>();

        handler = new AdminHandler(
            loggerMock.Object,
            goalServiceMock.Object,
            adminStateServiceMock.Object);
    }

    /// <summary>
    /// Tests that a message with null user triggers a warning log and returns without processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_NullUser_LogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, Text = "Test" };
        var cancellationToken = CancellationToken.None;

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received message with null user ID")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a message from a user without admin state triggers a warning log and returns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_NullState_LogsWarningAndReturns()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Test" };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(null as AdminGoalCreationState);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No admin state found for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a message with empty text triggers a warning log and returns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_EmptyMessageText_LogsWarningAndReturns()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForTitle };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received empty message text")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a valid title is processed correctly during the title step.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForTitle_ValidTitle_ProcessesTitleStep()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Новая цель" };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForTitle };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        adminStateServiceMock.Verify(
            x => x.SetTitle(123, "Новая цель"),
            Times.Once);
    }

    /// <summary>
    /// Tests that a title exceeding the length limit triggers cancellation of goal creation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForTitle_TooLongTitle_CancelsCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var longTitle = new string('a', 256);
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = longTitle };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForTitle };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("provided title that exceeds length limit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a valid description is processed correctly during the description step.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForDescription_ProcessesDescriptionStep()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Описание цели" };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForDescription };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        adminStateServiceMock.Verify(
            x => x.SetDescription(123, "Описание цели"),
            Times.Once);
    }

    /// <summary>
    /// Tests that an invalid amount triggers cancellation of goal creation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForAmount_InvalidAmount_CancelsCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "invalid_amount" };
        var state = new AdminGoalCreationState
        {
            CurrentStep = AdminGoalStep.WaitingForAmount,
            Title = "Test Goal",
            Description = "Test Description",
        };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("provided invalid amount")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that an amount exceeding the maximum limit triggers cancellation of goal creation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForAmount_AmountTooLarge_CancelsCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "100000000" };
        var state = new AdminGoalCreationState
        {
            CurrentStep = AdminGoalStep.WaitingForAmount,
            Title = "Test Goal",
            Description = "Test Description",
        };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);
    }

    /// <summary>
    /// Tests that a valid amount triggers successful goal creation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForAmount_ValidAmount_CreatesGoal()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "5000" };
        var state = new AdminGoalCreationState
        {
            CurrentStep = AdminGoalStep.WaitingForAmount,
            Title = "Test Goal",
            Description = "Test Description",
        };
        var cancellationToken = CancellationToken.None;

        var createdGoal = new Data.Models.DonationGoal { Id = 1, Title = "Test Goal", Description = "Test Description", TargetAmount = 5000 };
        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        goalServiceMock
            .Setup(x => x.CreateGoalAsync("Test Goal", "Test Description", 5000))
            .ReturnsAsync(createdGoal);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        goalServiceMock.Verify(
            x => x.CreateGoalAsync("Test Goal", "Test Description", 5000),
            Times.Once);

        adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Goal created successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that an exception in goal service triggers error logging.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForAmount_GoalServiceThrows_LogsError()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "5000" };
        var state = new AdminGoalCreationState
        {
            CurrentStep = AdminGoalStep.WaitingForAmount,
            Title = "Test Goal",
            Description = "Test Description",
        };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        goalServiceMock
            .Setup(x => x.CreateGoalAsync("Test Goal", "Test Description", 5000))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Failed to create goal in database")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that starting goal creation sets the admin state and logs appropriately.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task StartGoalCreationAsync_SetsStateAndLogs()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var cancellationToken = CancellationToken.None;

        // Act
        await handler.StartGoalCreationAsync(botClientMock.Object, chatId, userId, cancellationToken);

        // Assert
        adminStateServiceMock.Verify(
            x => x.StartGoalCreation(123, 456),
            Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Starting goal creation process")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that an exception during goal creation start is properly logged.
    /// </summary>
    [Test]
    public void StartGoalCreationAsyncThrowsExceptionLogsError()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.StartGoalCreation(123, 456))
            .Throws(new Exception("State service error"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() =>
            handler.StartGoalCreationAsync(botClientMock.Object, chatId, userId, cancellationToken));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Failed to start goal creation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that non-admin access attempts are properly logged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleNotAdmin_LogsWarning()
    {
        // Arrange
        var chatId = 456L;
        var cancellationToken = CancellationToken.None;

        // Act
        await handler.HandleNotAdmin(botClientMock.Object, chatId, cancellationToken);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Non-admin access attempt detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that an unknown admin step triggers a warning log.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAdminGoalCreationAsync_UnknownStep_LogsWarning()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Test" };
        var state = new AdminGoalCreationState { CurrentStep = (AdminGoalStep)999 };
        var cancellationToken = CancellationToken.None;

        adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await handler.HandleAdminGoalCreationAsync(botClientMock.Object, message, cancellationToken);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Unknown admin step")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}