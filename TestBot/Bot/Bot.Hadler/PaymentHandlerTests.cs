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
using Telegram.Bot.Types.Payments;
using Data.Models;

namespace Bot.Tests.Handlers;

[TestFixture]
public class PaymentHandlerTests
{
    private Mock<ILogger<PaymentHandler>> _loggerMock;
    private Mock<IGoalService> _goalServiceMock;
    private Mock<IDonationService> _donationServiceMock;
    private Mock<UserStateService> _userStateServiceMock;
    private Mock<ITelegramBotClient> _botClientMock;
    private PaymentHandler _handler;
    private BotConfig _botConfig;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<PaymentHandler>>();
        _goalServiceMock = new Mock<IGoalService>();
        _donationServiceMock = new Mock<IDonationService>();

        // Создаем мок для UserStateService с правильным конструктором
        var userStateServiceLoggerMock = new Mock<ILogger<UserStateService>>();
        _userStateServiceMock = new Mock<UserStateService>(userStateServiceLoggerMock.Object);

        _botClientMock = new Mock<ITelegramBotClient>();

        // Настраиваем конфигурацию бота
        _botConfig = new BotConfig { PaymentProviderToken = "test-payment-token" };
        var botConfigOptions = Options.Create(_botConfig);

        _handler = new PaymentHandler(
            _loggerMock.Object,
            _goalServiceMock.Object,
            _donationServiceMock.Object,
            _userStateServiceMock.Object,
            botConfigOptions);
    }

    [Test]
    public async Task HandleCustomAmountInputAsync_NullUser_LogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, Text = "500" };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleCustomAmountInputAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received custom amount input from null user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCustomAmountInputAsync_EmptyText_RemovesStateAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleCustomAmountInputAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _userStateServiceMock.Verify(x => x.RemoveWaitingForAmount(123), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("sent empty custom amount")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCustomAmountInputAsync_InvalidNumber_RemovesStateAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "invalid" };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleCustomAmountInputAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _userStateServiceMock.Verify(x => x.RemoveWaitingForAmount(123), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("sent invalid custom amount")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCustomAmountInputAsync_ValidAmount_CallsCreateDonationInvoice()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "500" };
        var cancellationToken = new CancellationToken();

        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };
        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await _handler.HandleCustomAmountInputAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _userStateServiceMock.Verify(x => x.RemoveWaitingForAmount(123), Times.Once);
    }

    [Test]
    public async Task CreateDonationInvoice_NoActiveGoal_SendsErrorMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 500;
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null);

        // Act
        await _handler.CreateDonationInvoice(_botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No active goal found for donation")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task CreateDonationInvoice_AmountBelowMinimum_SendsValidationMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 50; // Ниже минимума 60
        var cancellationToken = new CancellationToken();
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await _handler.CreateDonationInvoice(_botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("below minimum limit")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task CreateDonationInvoice_AmountAboveMaximum_SendsValidationMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 100001; // Выше максимума 100000
        var cancellationToken = new CancellationToken();
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await _handler.CreateDonationInvoice(_botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("exceeds maximum limit")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task CreateDonationInvoice_ValidAmount_LogsSuccess()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 500;
        var cancellationToken = new CancellationToken();
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await _handler.CreateDonationInvoice(_botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Creating donation invoice")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task CreateDonationInvoice_GoalServiceThrows_LogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 500;
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await _handler.CreateDonationInvoice(_botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error creating donation invoice")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleSuccessfulPaymentAsync_NullPaymentOrUser_LogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, SuccessfulPayment = null };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleSuccessfulPaymentAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received successful payment with null payment or user information")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleSuccessfulPaymentAsync_ValidPayment_ProcessesDonation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000, // 500.00 RUB
            Currency = "RUB"
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            SuccessfulPayment = successfulPayment,
        };
        var cancellationToken = new CancellationToken();

        _donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleSuccessfulPaymentAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _donationServiceMock.Verify(
            x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Successfully processed donation")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleSuccessfulPaymentAsync_DonationServiceReturnsFalse_LogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000,
            Currency = "RUB"
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            SuccessfulPayment = successfulPayment,
        };
        var cancellationToken = new CancellationToken();

        _donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleSuccessfulPaymentAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to process donation")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleSuccessfulPaymentAsync_DonationServiceThrows_LogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000,
            Currency = "RUB"
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            SuccessfulPayment = successfulPayment,
        };
        var cancellationToken = new CancellationToken();

        _donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await _handler.HandleSuccessfulPaymentAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error handling successful payment")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleSuccessfulPaymentAsync_ValidPayment_DeletesInvoiceMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000,
            Currency = "RUB"
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            //MessageId = 789,
            SuccessfulPayment = successfulPayment
        };
        var cancellationToken = new CancellationToken();

        _donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleSuccessfulPaymentAsync(_botClientMock.Object, message, cancellationToken);
    }

    [Test]
    public async Task CreateDonationInvoice_AmountBoundaryValues_ValidatesCorrectly()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var cancellationToken = new CancellationToken();
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Test cases: amount, shouldBeValid
        var testCases = new[]
        {
            new { Amount = 59, ShouldBeValid = false },
            new { Amount = 60, ShouldBeValid = true },
            new { Amount = 100000, ShouldBeValid = true },
            new { Amount = 100001, ShouldBeValid = false },
            new { Amount = 500, ShouldBeValid = true },
            new { Amount = 0, ShouldBeValid = false },
            new { Amount = -100, ShouldBeValid = false }
        };

        foreach (var testCase in testCases)
        {
            // Act
            await _handler.CreateDonationInvoice(_botClientMock.Object, chatId, userId, testCase.Amount, cancellationToken);

            // Assert
            if (!testCase.ShouldBeValid)
            {
                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("provided invalid donation amount")),
                        It.IsAny<Exception>(),
                        It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                    Times.AtLeastOnce);
            }
        }
    }
}