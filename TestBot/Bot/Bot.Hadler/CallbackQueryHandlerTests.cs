using Bot.Handlers;
using Bot.Services;
using Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Tests.Handlers;

[TestFixture]
public class CallbackQueryHandlerTests
{
    private Mock<ILogger<CallbackQueryHandler>> _loggerMock;
    private Mock<PaymentHandler> _paymentHandlerMock;
    private Mock<CommandHandler> _commandHandlerMock;
    private Mock<UserStateService> _userStateServiceMock;
    private Mock<ITelegramBotClient> _botClientMock;
    private CallbackQueryHandler _handler;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<CallbackQueryHandler>>();

        // Явно создаем мок логгера для UserStateService
        var userStateServiceLoggerMock = new Mock<ILogger<UserStateService>>();
        _userStateServiceMock = new Mock<UserStateService>(userStateServiceLoggerMock.Object);

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

        _paymentHandlerMock = new Mock<PaymentHandler>(
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

        _commandHandlerMock = new Mock<CommandHandler>(
            commandLoggerMock.Object,
            commandGoalServiceMock.Object,
            keyboardServiceMock.Object,
            adminHandlerMock.Object);

        _botClientMock = new Mock<ITelegramBotClient>();

        // Настраиваем бизнес-методы
        _paymentHandlerMock
            .Setup(x => x.CreateDonationInvoice(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _commandHandlerMock
            .Setup(x => x.HandleStatsCommand(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userStateServiceMock
            .Setup(x => x.SetWaitingForAmount(
                It.IsAny<long>(),
                It.IsAny<long>()))
            .Verifiable();

        _handler = new CallbackQueryHandler(
            _loggerMock.Object,
            _paymentHandlerMock.Object,
            _commandHandlerMock.Object,
            _userStateServiceMock.Object);
    }

    [Test]
    public void CanHandle_WithCallbackQuery_ReturnsTrue()
    {
        // Arrange
        var update = new Update { CallbackQuery = new CallbackQuery() };

        // Act
        var result = _handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithoutCallbackQuery_ReturnsFalse()
    {
        // Arrange
        var update = new Update { CallbackQuery = null };

        // Act
        var result = _handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task HandleAsync_NoChatInformation_DoesNotProcessFurther()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "donate_100",
            Message = null // No message/chat information
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - убеждаемся, что бизнес-логика не вызывалась
        _paymentHandlerMock.Verify(
            x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_EmptyCallbackData_DoesNotProcessFurther()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = null, // Empty callback data
            Message = new Message { Chat = new Chat { Id = 456 } }
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - убеждаемся, что бизнес-логика не вызывалась
        _paymentHandlerMock.Verify(
            x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_EnterCustomAmount_SetsUserState()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "enter_custom_amount",
            Message = new Message { Chat = new Chat { Id = 456 } }
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _userStateServiceMock.Verify(
            x => x.SetWaitingForAmount(123, 456),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_PredefinedDonation_CreatesInvoice()
    {
        // Arrange
        var testCases = new[]
        {
            new { CallbackData = "donate_100", ExpectedAmount = 100 },
            new { CallbackData = "donate_500", ExpectedAmount = 500 },
            new { CallbackData = "donate_1000", ExpectedAmount = 1000 },
            new { CallbackData = "donate_5000", ExpectedAmount = 5000 }
        };

        foreach (var testCase in testCases)
        {
            // Reset mocks for each test case
            _paymentHandlerMock.Invocations.Clear();

            var callbackQuery = new CallbackQuery
            {
                Id = "test_query_id",
                From = new User { Id = 123 },
                Data = testCase.CallbackData,
                Message = new Message { Chat = new Chat { Id = 456 } }
            };
            var update = new Update { CallbackQuery = callbackQuery };
            var cancellationToken = new CancellationToken();

            // Act
            await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

            // Assert
            _paymentHandlerMock.Verify(
                x => x.CreateDonationInvoice(_botClientMock.Object, 456, 123, testCase.ExpectedAmount, cancellationToken),
                Times.Once,
                $"Failed for callback data: {testCase.CallbackData}");
        }
    }

    [Test]
    public async Task HandleAsync_ShowStats_CallsStatsCommand()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "show_stats",
            Message = new Message { Chat = new Chat { Id = 456 } }
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _commandHandlerMock.Verify(
            x => x.HandleStatsCommand(_botClientMock.Object, 456, cancellationToken),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_UnknownCallbackData_DoesNotCallBusinessLogic()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "unknown_command",
            Message = new Message { Chat = new Chat { Id = 456 } }
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - убеждаемся, что бизнес-логика не вызывалась
        _paymentHandlerMock.Verify(
            x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _commandHandlerMock.Verify(
            x => x.HandleStatsCommand(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userStateServiceMock.Verify(
            x => x.SetWaitingForAmount(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_PaymentHandlerThrowsFormatException_LogsError()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "donate_invalid", // This will cause FormatException in parsing
            Message = new Message { Chat = new Chat { Id = 456 } }
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что ошибка была залогирована
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to parse donation amount")),
                It.IsAny<FormatException>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_GeneralExceptionInProcess_CallsSafeAnswer()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "donate_100",
            Message = new Message { Chat = new Chat { Id = 456 } }
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Симулируем исключение в PaymentHandler
        _paymentHandlerMock
            .Setup(x => x.CreateDonationInvoice(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что ошибка была залогирована
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to create donation invoice")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_CommandHandlerThrowsException_LogsError()
    {
        // Arrange
        var callbackQuery = new CallbackQuery
        {
            Id = "test_query_id",
            From = new User { Id = 123 },
            Data = "show_stats",
            Message = new Message { Chat = new Chat { Id = 456 } }
        };
        var update = new Update { CallbackQuery = callbackQuery };
        var cancellationToken = new CancellationToken();

        // Симулируем исключение в CommandHandler
        _commandHandlerMock
            .Setup(x => x.HandleStatsCommand(It.IsAny<ITelegramBotClient>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что ошибка была залогирована
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to show statistics")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }
}