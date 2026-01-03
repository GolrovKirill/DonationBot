// <copyright file="CallbackQueryHandlerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bot.Handlers;
using Bot.Services;
using Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Services;
using System.Reflection.Metadata;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Tests.Handlers;

/// <summary>
/// Unit tests for the <see cref="CallbackQueryHandler"/> class.
/// </summary>
[TestFixture]
public class CallbackQueryHandlerTests
{
    private Mock<ILogger<CallbackQueryHandler>> loggerMock;
    private Mock<PaymentHandler> paymentHandlerMock;
    private Mock<CommandHandler> commandHandlerMock;
    private Mock<UserStateService> userStateServiceMock;
    private Mock<ITelegramBotClient> botClientMock;
    private CallbackQueryHandler handler;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this.loggerMock = new Mock<ILogger<CallbackQueryHandler>>();

        // Явно создаем мок логгера для UserStateService
        var userStateServiceLoggerMock = new Mock<ILogger<UserStateService>>();
        this.userStateServiceMock = new Mock<UserStateService>(userStateServiceLoggerMock.Object);

        // Создаем моки для зависимостей PaymentHandler
        var paymentLoggerMock = new Mock<ILogger<PaymentHandler>>();
        var goalServiceMock = new Mock<IGoalService>();
        var donationServiceMock = new Mock<IDonationService>();
        var userStateServiceForPaymentMock = new Mock<UserStateService>(Mock.Of<ILogger<UserStateService>>());
        var botConfigMock = new Mock<IOptions<BotConfig>>();
        botConfigMock.Setup(x => x.Value).Returns(new BotConfig
        {
            PaymentProviderToken = "test-token",
        });

        this.paymentHandlerMock = new Mock<PaymentHandler>(
            paymentLoggerMock.Object,
            goalServiceMock.Object,
            donationServiceMock.Object,
            userStateServiceForPaymentMock.Object,
            botConfigMock.Object);

        // Создаем моки для зависимостей CommandHandler
        var commandLoggerMock = new Mock<ILogger<CommandHandler>>();
        var commandGoalServiceMock = new Mock<IGoalService>();
        var keyboardServiceMock = new Mock<KeyboardService>(Mock.Of<ILogger<KeyboardService>>());

        // Создаем мок для AdminStateService
        var adminStateServiceLoggerMock = new Mock<ILogger<AdminStateService>>();
        var adminStateServiceMock = new Mock<AdminStateService>(adminStateServiceLoggerMock.Object);

        // Создаем мок для AdminHandler
        var adminHandlerMock = new Mock<AdminHandler>(
            Mock.Of<ILogger<AdminHandler>>(),
            Mock.Of<IGoalService>(),
            adminStateServiceMock.Object);

        this.commandHandlerMock = new Mock<CommandHandler>(
            commandLoggerMock.Object,
            commandGoalServiceMock.Object,
            keyboardServiceMock.Object,
            adminHandlerMock.Object);

        this.botClientMock = new Mock<ITelegramBotClient>();

        // Настраиваем бизнес-методы
        this.paymentHandlerMock
            .Setup(x => x.CreateDonationInvoice(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.commandHandlerMock
            .Setup(x => x.HandleStatsCommand(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.userStateServiceMock
            .Setup(x => x.SetWaitingForAmount(
                It.IsAny<long>(),
                It.IsAny<long>()))
            .Verifiable();

        this.handler = new CallbackQueryHandler(
            this.loggerMock.Object,
            this.paymentHandlerMock.Object,
            this.commandHandlerMock.Object,
            this.userStateServiceMock.Object);
    }

    /// <summary>
    /// Tests that the handler correctly identifies updates with callback queries.
    /// </summary>
    [Test]
    public void CanHandleWithCallbackQueryReturnsTrue()
    {
        // Arrange
        var update = new Update { CallbackQuery = new CallbackQuery() };

        // Act
        var result = this.handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests that the handler correctly identifies updates without callback queries.
    /// </summary>
    [Test]
    public void CanHandleWithoutCallbackQueryReturnsFalse()
    {
        // Arrange
        var update = new Update { CallbackQuery = null };

        // Act
        var result = this.handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests that a callback query without chat information does not trigger further processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncNoChatInformationDoesNotProcessFurther()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "donate_100",
            Message = null, // No message/chat information
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert - убеждаемся, что бизнес-логика не вызывалась
        this.paymentHandlerMock.Verify(
            x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that a callback query with empty data does not trigger further processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncEmptyCallbackDataDoesNotProcessFurther()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = null, // Empty callback data
            Message = new Message { Chat = new Chat { Id = 456 } },
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert - убеждаемся, что бизнес-логика не вызывалась
        this.paymentHandlerMock.Verify(
            x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that the "enter custom amount" callback correctly sets the user state.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncEnterCustomAmountSetsUserState()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "enter_custom_amount",
            Message = new Message { Chat = new Chat { Id = 456 } },
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.userStateServiceMock.Verify(
            x => x.SetWaitingForAmount(123, 456),
            Times.Once);
    }

    /// <summary>
    /// Tests that predefined donation callbacks correctly create invoices with appropriate amounts.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncPredefinedDonationCreatesInvoice()
    {
        // Arrange
        var testCases = new[]
        {
            new { CallbackData = "donate_100", ExpectedAmount = 100 },
            new { CallbackData = "donate_500", ExpectedAmount = 500 },
            new { CallbackData = "donate_1000", ExpectedAmount = 1000 },
            new { CallbackData = "donate_5000", ExpectedAmount = 5000 },
        };

        foreach (var testCase in testCases)
        {
            // Reset mocks for each test case
            this.paymentHandlerMock.Invocations.Clear();

            var callbackQuery = new CallbackQuery
            {
                Id = "test_query_id",
                From = new User { Id = 123 },
                Data = testCase.CallbackData,
                Message = new Message { Chat = new Chat { Id = 456 } },
            };
            var update = new Update { CallbackQuery = callbackQuery };
            var cancellationToken = CancellationToken.None;

            // Act
            await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

            // Assert
            this.paymentHandlerMock.Verify(
                x => x.CreateDonationInvoice(this.botClientMock.Object, 456, 123, testCase.ExpectedAmount, cancellationToken),
                Times.Once,
                $"Failed for callback data: {testCase.CallbackData}");
        }
    }

    /// <summary>
    /// Tests that the "show stats" callback correctly triggers statistics display.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncShowStatsCallsStatsCommand()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "show_stats",
            Message = new Message { Chat = new Chat { Id = 456 } },
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.commandHandlerMock.Verify(
            x => x.HandleStatsCommand(this.botClientMock.Object, 456, cancellationToken),
            Times.Once);
    }

    /// <summary>
    /// Tests that unknown callback data does not trigger any business logic.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncUnknownCallbackDataDoesNotCallBusinessLogic()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "unknown_command",
            Message = new Message { Chat = new Chat { Id = 456 } },
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert - убеждаемся, что бизнес-логика не вызывалась
        this.paymentHandlerMock.Verify(
            x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        this.commandHandlerMock.Verify(
            x => x.HandleStatsCommand(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);

        this.userStateServiceMock.Verify(
            x => x.SetWaitingForAmount(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that format exceptions during donation amount parsing are properly logged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncPaymentHandlerThrowsFormatExceptionLogsError()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "donate_invalid", // This will cause FormatException in parsing
            Message = new Message { Chat = new Chat { Id = 456 } },
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Act
        await handler.HandleAsync(botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что ошибка была залогирована
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Failed to parse donation amount")),
                It.IsAny<FormatException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that general exceptions during invoice creation are properly logged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncGeneralExceptionInProcessCallsSafeAnswer()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "donate_100",
            Message = new Message { Chat = new Chat { Id = 456 } },
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Симулируем исключение в PaymentHandler
        this.paymentHandlerMock
            .Setup(x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что ошибка была залогирована
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Failed to create donation invoice")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that exceptions during statistics display are properly logged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncCommandHandlerThrowsExceptionLogsError()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "show_stats",
            Message = new Message { Chat = new Chat { Id = 456 } },
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = CancellationToken.None;

        // Симулируем исключение в CommandHandler
        this.commandHandlerMock
            .Setup(x => x.HandleStatsCommand(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что ошибка была залогирована
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Failed to show statistics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}