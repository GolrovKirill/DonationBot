// <copyright file="CommandHandlerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bot.Handlers;
using Bot.Services;
using Data.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Tests.Handlers;

/// <summary>
/// Unit tests for the <see cref="CommandHandler"/> class.
/// </summary>
[TestFixture]
public class CommandHandlerTests
{
    private Mock<ILogger<CommandHandler>> loggerMock;
    private Mock<IGoalService> goalServiceMock;
    private Mock<KeyboardService> keyboardServiceMock;
    private Mock<AdminHandler> adminHandlerMock;
    private Mock<ITelegramBotClient> botClientMock;
    private CommandHandler handler;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this.loggerMock = new Mock<ILogger<CommandHandler>>();
        this.goalServiceMock = new Mock<IGoalService>();

        var keyboardServiceLoggerMock = new Mock<ILogger<KeyboardService>>();
        this.keyboardServiceMock = new Mock<KeyboardService>(keyboardServiceLoggerMock.Object);

        var adminHandlerLoggerMock = new Mock<ILogger<AdminHandler>>();
        var adminStateServiceMock = new Mock<AdminStateService>(Mock.Of<ILogger<AdminStateService>>());
        this.adminHandlerMock = new Mock<AdminHandler>(
            adminHandlerLoggerMock.Object,
            Mock.Of<IGoalService>(),
            adminStateServiceMock.Object);

        this.botClientMock = new Mock<ITelegramBotClient>();

        this.handler = new CommandHandler(
            this.loggerMock.Object,
            this.goalServiceMock.Object,
            this.keyboardServiceMock.Object,
            this.adminHandlerMock.Object);
    }

    /// <summary>
    /// Tests that a command from a null user triggers a warning log and returns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncNullUserLogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, Text = "/start" };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received command from message with null user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that an empty message text triggers a warning log and returns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncEmptyMessageTextLogsWarningAndReturns()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received empty message text")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the start command for a regular user sends the main menu.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncStartCommandRegularUserSendsMainMenu()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var cancellationToken = CancellationToken.None;
        var startStats = "Статистика: 5000/10000";
        var keyboard = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "💳 Пожертвовать" } });

        this.goalServiceMock
            .Setup(x => x.GetStartStats())
            .ReturnsAsync(startStats);

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        this.keyboardServiceMock
            .Setup(x => x.GetMainMenuKeyboard())
            .Returns(keyboard);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.GetStartStats(), Times.Once);
        this.goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);
        this.keyboardServiceMock.Verify(x => x.GetMainMenuKeyboard(), Times.Once);
        this.keyboardServiceMock.Verify(x => x.GetMainMenuKeyboardForAdmin(), Times.Never);
    }

    /// <summary>
    /// Tests that the start command for an admin user sends the admin menu.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncStartCommandAdminUserSendsAdminMenu()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var cancellationToken = CancellationToken.None;
        var startStats = "Статистика: 5000/10000";
        var keyboard = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "📝 Создать новую цель" } });

        this.goalServiceMock
            .Setup(x => x.GetStartStats())
            .ReturnsAsync(startStats);

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        this.keyboardServiceMock
            .Setup(x => x.GetMainMenuKeyboardForAdmin())
            .Returns(keyboard);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);
        this.keyboardServiceMock.Verify(x => x.GetMainMenuKeyboardForAdmin(), Times.Once);
        this.keyboardServiceMock.Verify(x => x.GetMainMenuKeyboard(), Times.Never);
    }

    /// <summary>
    /// Tests that the donate command with an active goal sends the donation keyboard.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncDonateCommandWithActiveGoalSendsDonationKeyboard()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/donate" };
        var cancellationToken = CancellationToken.None;
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var keyboard = new InlineKeyboardMarkup(new[] { new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData("100") } });

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        this.keyboardServiceMock
            .Setup(x => x.GetDonationAmountKeyboard())
            .Returns(keyboard);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        this.keyboardServiceMock.Verify(x => x.GetDonationAmountKeyboard(), Times.Once);
    }

    /// <summary>
    /// Tests that the donate command without an active goal sends an error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncDonateCommandNoActiveGoalSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/donate" };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(null as DonationGoal);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        this.keyboardServiceMock.Verify(x => x.GetDonationAmountKeyboard(), Times.Never);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No active goal found for donate command")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the stats command calls the appropriate handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncStatsCommandCallsHandleStatsCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/stats" };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);
    }

    /// <summary>
    /// Tests that the stats command handler successfully retrieves and sends statistics.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleStatsCommandSuccessfulLogsAndSendsStats()
    {
        // Arrange
        var chatId = 456L;
        var cancellationToken = CancellationToken.None;
        var stats = "Статистика: 5000/10000";

        this.goalServiceMock
            .Setup(x => x.GetGoalStatsAsync())
            .ReturnsAsync(stats);

        // Act
        await this.handler.HandleStatsCommand(this.botClientMock.Object, chatId, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.GetGoalStatsAsync(), Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Processing stats command")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Statistics sent successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that exceptions during stats command handling are properly logged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleStatsCommandThrowsExceptionLogsError()
    {
        // Arrange
        var chatId = 456L;
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetGoalStatsAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await this.handler.HandleStatsCommand(this.botClientMock.Object, chatId, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error getting stats")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the add goal command from an admin user starts goal creation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncAddGoalCommandAdminUserStartsGoalCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/addgoal" };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.adminHandlerMock.Verify(
            x => x.StartGoalCreationAsync(this.botClientMock.Object, 456, 123, cancellationToken),
            Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Admin user") && v.ToString() !.Contains("starting goal creation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the add goal command from a non-admin user triggers the not admin handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncAddGoalCommandNonAdminUserHandlesNotAdmin()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/addgoal" };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.adminHandlerMock.Verify(
            x => x.HandleNotAdmin(this.botClientMock.Object, 456, cancellationToken),
            Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Non-admin user") && v.ToString() !.Contains("attempted to create goal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that an unknown command from an admin user sends admin help.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncUnknownCommandAdminUserSendsAdminHelp()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "unknown_command" };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Unknown command from user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that an unknown command from a regular user sends regular help.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncUnknownCommandRegularUserSendsRegularHelp()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "unknown_command" };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Unknown command from user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that various start command variants all call the start command handler.
    /// </summary>
    /// <param name="command">The command variant to test.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TestCase("/start")]
    [TestCase("🔄 Обновить")]
    [TestCase("Обновить")]
    public async Task HandleCommandAsyncStartCommandVariantsCallsHandleStartCommand(string command)
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = command };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetStartStats())
            .ReturnsAsync("Статистика");

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        this.keyboardServiceMock
            .Setup(x => x.GetMainMenuKeyboard())
            .Returns(new ReplyKeyboardMarkup(new KeyboardButton[0][]));

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.GetStartStats(), Times.Once);
    }

    /// <summary>
    /// Tests that various donate command variants all call the donate command handler.
    /// </summary>
    /// <param name="command">The command variant to test.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TestCase("/donate")]
    [TestCase("💳 Пожертвовать")]
    [TestCase("Пожертвовать")]
    public async Task HandleCommandAsyncDonateCommandVariantsCallsHandleDonateCommand(string command)
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = command };
        var cancellationToken = CancellationToken.None;
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        this.keyboardServiceMock
            .Setup(x => x.GetDonationAmountKeyboard())
            .Returns(new InlineKeyboardMarkup(new InlineKeyboardButton[0][]));

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        this.keyboardServiceMock.Verify(x => x.GetDonationAmountKeyboard(), Times.Once);
    }

    /// <summary>
    /// Tests that various stats command aliases all call the stats command handler.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncStatsCommandAliasesAllCallHandleStatsCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var aliases = new[] { "/stats", "📊 Статистика", "Статистика" };
        var cancellationToken = CancellationToken.None;

        foreach (var alias in aliases)
        {
            var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = alias };

            // Act
            await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);
        }
    }

    /// <summary>
    /// Tests that various add goal command aliases all call the add goal command handler.
    /// </summary>
    /// <param name="command">The command variant to test.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [TestCase("/addgoal")]
    [TestCase("📝 Создать новую цель")]
    [TestCase("Создать новую цель")]
    public async Task HandleCommandAsyncAddGoalCommandAliasesAllCallHandleAddGoalCommand(string command)
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = command };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);
    }

    /// <summary>
    /// Tests that exceptions during command handling are properly logged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCommandAsyncThrowsExceptionLogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetStartStats())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await this.handler.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error processing command")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}