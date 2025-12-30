using Bot.Handlers;
using Bot.Services;
using Configurations;
using Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;

namespace Bot.Tests.Handlers;

[TestFixture]
public class MessageHandlerTests
{
    private Mock<ILogger<MessageHandler>> _loggerMock;
    private Mock<IDonationService> _donationServiceMock;
    private Mock<IGoalService> _goalServiceMock;
    private Mock<CommandHandler> _commandHandlerMock;
    private Mock<PaymentHandler> _paymentHandlerMock;
    private Mock<UserStateService> _userStateServiceMock;
    private Mock<AdminHandler> _adminHandlerMock;
    private Mock<AdminStateService> _adminStateServiceMock;
    private Mock<ITelegramBotClient> _botClientMock;
    private MessageHandler _handler;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MessageHandler>>();
        _donationServiceMock = new Mock<IDonationService>();
        _goalServiceMock = new Mock<IGoalService>();

        // Явно создаем моки для всех зависимостей, как в предыдущем SetUp

        // Создаем мок логгера для UserStateService
        var userStateServiceLoggerMock = new Mock<ILogger<UserStateService>>();
        _userStateServiceMock = new Mock<UserStateService>(userStateServiceLoggerMock.Object);

        // Создаем моки для зависимостей PaymentHandler (как в предыдущем SetUp)
        var paymentLoggerMock = new Mock<ILogger<PaymentHandler>>();
        var paymentGoalServiceMock = new Mock<IGoalService>();
        var paymentDonationServiceMock = new Mock<IDonationService>();
        var userStateServiceForPaymentMock = new Mock<UserStateService>(Mock.Of<ILogger<UserStateService>>());
        var botConfigMock = new Mock<IOptions<BotConfig>>();
        botConfigMock.Setup(x => x.Value).Returns(new BotConfig
        {
            PaymentProviderToken = "test-token",
        });

        _paymentHandlerMock = new Mock<PaymentHandler>(
            paymentLoggerMock.Object,
            paymentGoalServiceMock.Object,
            paymentDonationServiceMock.Object,
            userStateServiceForPaymentMock.Object,
            botConfigMock.Object);

        // Создаем моки для зависимостей CommandHandler
        var commandLoggerMock = new Mock<ILogger<CommandHandler>>();
        var commandGoalServiceMock = new Mock<IGoalService>();
        var keyboardServiceMock = new Mock<KeyboardService>(Mock.Of<ILogger<KeyboardService>>());

        // Создаем мок для AdminStateService
        var adminStateServiceLoggerMock = new Mock<ILogger<AdminStateService>>();
        _adminStateServiceMock = new Mock<AdminStateService>(adminStateServiceLoggerMock.Object);

        // Создаем мок для AdminHandler
        _adminHandlerMock = new Mock<AdminHandler>(
            Mock.Of<ILogger<AdminHandler>>(),
            Mock.Of<IGoalService>(),
            _adminStateServiceMock.Object);

        _commandHandlerMock = new Mock<CommandHandler>(
            commandLoggerMock.Object,
            commandGoalServiceMock.Object,
            keyboardServiceMock.Object,
            _adminHandlerMock.Object);

        _botClientMock = new Mock<ITelegramBotClient>();

        // Настраиваем базовые методы
        _donationServiceMock
            .Setup(x => x.GetOrCreateUserAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.FromResult(new Data.Models.Users
            {
                Id = 123,
                Username = "testuser",
                FirstName = "Test",
                LastName = "User"
            })); // Исправлено - возвращаем User

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(It.IsAny<long>()))
            .ReturnsAsync(false);

        _commandHandlerMock
            .Setup(x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentHandlerMock
            .Setup(x => x.HandleSuccessfulPaymentAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _paymentHandlerMock
            .Setup(x => x.HandleCustomAmountInputAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _adminHandlerMock
            .Setup(x => x.HandleAdminGoalCreationAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new MessageHandler(
            _loggerMock.Object,
            _donationServiceMock.Object,
            _goalServiceMock.Object,
            _commandHandlerMock.Object,
            _paymentHandlerMock.Object,
            _userStateServiceMock.Object,
            _adminHandlerMock.Object,
            _adminStateServiceMock.Object);
    }

    [Test]
    public void CanHandle_WithMessage_ReturnsTrue()
    {
        // Arrange
        var update = new Update { Message = new Message() };

        // Act
        var result = _handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithoutMessage_ReturnsFalse()
    {
        // Arrange
        var update = new Update { Message = null };

        // Act
        var result = _handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task HandleAsync_MessageWithNullUser_LogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 } };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received message with null user information")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);

        // Проверяем, что бизнес-логика не вызывалась
        _donationServiceMock.Verify(
            x => x.GetOrCreateUserAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_ValidMessage_RegistersOrUpdatesUser()
    {
        // Arrange
        var user = new User { Id = 123, Username = "testuser", FirstName = "Test", LastName = "User" };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "test" };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _donationServiceMock.Verify(
            x => x.GetOrCreateUserAsync(123, "testuser", "Test", "User"),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_SuccessfulPayment_ProcessesPayment()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment();
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, SuccessfulPayment = successfulPayment };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _paymentHandlerMock.Verify(
            x => x.HandleSuccessfulPaymentAsync(_botClientMock.Object, message, cancellationToken),
            Times.Once);

        // Проверяем, что дальнейшая обработка не происходит
        _commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_UserWaitingForAmount_ProcessesCustomAmount()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "500" };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        _userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(true);

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _paymentHandlerMock.Verify(
            x => x.HandleCustomAmountInputAsync(_botClientMock.Object, message, cancellationToken),
            Times.Once);

        // Проверяем, что обычная обработка команд не происходит
        _commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_AdminCreatingGoal_ProcessesGoalCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Новая цель" };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        _userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        _adminStateServiceMock
            .Setup(x => x.IsUserCreatingGoal(123))
            .Returns(true);

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _adminHandlerMock.Verify(
            x => x.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken),
            Times.Once);

        // Проверяем, что обычная обработка команд не происходит
        _commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_RegularTextMessage_ProcessesCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        _userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _commandHandlerMock.Verify(
            x => x.HandleCommandAsync(_botClientMock.Object, message, cancellationToken),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_NonTextMessage_DoesNotProcessCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null }; // Не текстовое сообщение
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        _userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_UserRegistrationFails_LogsErrorButContinues()
    {
        // Arrange
        var user = new User { Id = 123, Username = "testuser", FirstName = "Test", LastName = "User" };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        _donationServiceMock
            .Setup(x => x.GetOrCreateUserAsync(123, "testuser", "Test", "User"))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to register/update user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);

        // Проверяем, что обработка команды все равно происходит
        _commandHandlerMock.Verify(
            x => x.HandleCommandAsync(_botClientMock.Object, message, cancellationToken),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_CommandHandlerThrowsException_LogsError()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        _userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        _commandHandlerMock
            .Setup(x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Command processing failed"));

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing text message")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_AdminNotCreatingGoal_ProcessesAsRegularCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = new CancellationToken();

        _userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true); // Пользователь - админ

        _adminStateServiceMock
            .Setup(x => x.IsUserCreatingGoal(123))
            .Returns(false); // Но не создает цель

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _adminHandlerMock.Verify(
            x => x.HandleAdminGoalCreationAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _commandHandlerMock.Verify(
            x => x.HandleCommandAsync(_botClientMock.Object, message, cancellationToken),
            Times.Once);
    }
}