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

/// <summary>
/// Unit tests for the <see cref="PaymentHandler"/> class.
/// </summary>
[TestFixture]
public class PaymentHandlerTests
{
    private Mock<ILogger<PaymentHandler>> loggerMock;
    private Mock<IGoalService> goalServiceMock;
    private Mock<IDonationService> donationServiceMock;
    private Mock<UserStateService> userStateServiceMock;
    private Mock<ITelegramBotClient> botClientMock;
    private PaymentHandler handler;
    private BotConfig botConfig;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this.loggerMock = new Mock<ILogger<PaymentHandler>>();
        this.goalServiceMock = new Mock<IGoalService>();
        this.donationServiceMock = new Mock<IDonationService>();

        // Create mock for UserStateService with correct constructor
        var userStateServiceLoggerMock = new Mock<ILogger<UserStateService>>();
        this.userStateServiceMock = new Mock<UserStateService>(userStateServiceLoggerMock.Object);

        this.botClientMock = new Mock<ITelegramBotClient>();

        // Configure bot configuration
        this.botConfig = new BotConfig { PaymentProviderToken = "test-payment-token" };
        var botConfigOptions = Options.Create(this.botConfig);

        this.handler = new PaymentHandler(
            this.loggerMock.Object,
            this.goalServiceMock.Object,
            this.donationServiceMock.Object,
            this.userStateServiceMock.Object,
            botConfigOptions);
    }

    /// <summary>
    /// Tests that custom amount input with null user triggers a warning log.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCustomAmountInputAsyncNullUserLogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, Text = "500" };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleCustomAmountInputAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received custom amount input from null user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that empty text input removes user state and sends error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCustomAmountInputAsyncEmptyTextRemovesStateAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleCustomAmountInputAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.userStateServiceMock.Verify(x => x.RemoveWaitingForAmount(123), Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("sent empty custom amount")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that invalid number input removes user state and sends error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCustomAmountInputAsyncInvalidNumberRemovesStateAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "invalid" };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleCustomAmountInputAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.userStateServiceMock.Verify(x => x.RemoveWaitingForAmount(123), Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("sent invalid custom amount")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that valid amount input calls CreateDonationInvoice method.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleCustomAmountInputAsyncValidAmountCallsCreateDonationInvoice()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "500" };
        var cancellationToken = CancellationToken.None;

        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };
        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await this.handler.HandleCustomAmountInputAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.userStateServiceMock.Verify(x => x.RemoveWaitingForAmount(123), Times.Once);
    }

    /// <summary>
    /// Tests that donation creation without active goal sends error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CreateDonationInvoiceNoActiveGoalSendsErrorMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 500;
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(null as DonationGoal);

        // Act
        await this.handler.CreateDonationInvoice(this.botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No active goal found for donation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that donation amount below minimum sends validation message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CreateDonationInvoiceAmountBelowMinimumSendsValidationMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 50; // Below minimum 60
        var cancellationToken = CancellationToken.None;
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await this.handler.CreateDonationInvoice(this.botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("below minimum limit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that donation amount above maximum sends validation message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CreateDonationInvoiceAmountAboveMaximumSendsValidationMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 100001; // Above maximum 100000
        var cancellationToken = CancellationToken.None;
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await this.handler.CreateDonationInvoice(this.botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("exceeds maximum limit")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that valid donation amount logs success.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CreateDonationInvoiceValidAmountLogsSuccess()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 500;
        var cancellationToken = CancellationToken.None;
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await this.handler.CreateDonationInvoice(this.botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Creating donation invoice")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that goal service exception logs error and sends error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CreateDonationInvoiceGoalServiceThrowsLogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var amount = 500;
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await this.handler.CreateDonationInvoice(this.botClientMock.Object, chatId, userId, amount, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error creating donation invoice")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that successful payment with null payment or user logs warning.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleSuccessfulPaymentAsyncNullPaymentOrUserLogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, SuccessfulPayment = null };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleSuccessfulPaymentAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received successful payment with null payment or user information")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that valid successful payment processes donation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleSuccessfulPaymentAsyncValidPaymentProcessesDonation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000, // 500.00 RUB
            Currency = "RUB",
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            SuccessfulPayment = successfulPayment,
        };
        var cancellationToken = CancellationToken.None;

        this.donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ReturnsAsync(true);

        // Act
        await this.handler.HandleSuccessfulPaymentAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.donationServiceMock.Verify(
            x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"),
            Times.Once);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Successfully processed donation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that donation service returning false logs error and sends error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleSuccessfulPaymentAsyncDonationServiceReturnsFalseLogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000,
            Currency = "RUB",
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            SuccessfulPayment = successfulPayment,
        };
        var cancellationToken = CancellationToken.None;

        this.donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ReturnsAsync(false);

        // Act
        await this.handler.HandleSuccessfulPaymentAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Failed to process donation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that donation service exception logs error and sends error message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleSuccessfulPaymentAsyncDonationServiceThrowsLogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000,
            Currency = "RUB",
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            SuccessfulPayment = successfulPayment,
        };
        var cancellationToken = CancellationToken.None;

        this.donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await this.handler.HandleSuccessfulPaymentAsync(this.botClientMock.Object, message, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error handling successful payment")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that valid successful payment deletes invoice message.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleSuccessfulPaymentAsyncValidPaymentDeletesInvoiceMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment
        {
            TelegramPaymentChargeId = "charge_123",
            TotalAmount = 50000,
            Currency = "RUB",
        };
        var message = new Message
        {
            From = user,
            Chat = new Chat { Id = 456 },
            SuccessfulPayment = successfulPayment,
        };
        var cancellationToken = CancellationToken.None;

        this.donationServiceMock
            .Setup(x => x.ProcessDonationAsync(123, 500, "RUB", "charge_123"))
            .ReturnsAsync(true);

        // Act
        await this.handler.HandleSuccessfulPaymentAsync(this.botClientMock.Object, message, cancellationToken);
    }

    /// <summary>
    /// Tests that donation amount boundary values are validated correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CreateDonationInvoiceAmountBoundaryValuesValidatesCorrectly()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var cancellationToken = CancellationToken.None;
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        this.goalServiceMock
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
            new { Amount = -100, ShouldBeValid = false },
        };

        foreach (var testCase in testCases)
        {
            // Act
            await this.handler.CreateDonationInvoice(this.botClientMock.Object, chatId, userId, testCase.Amount, cancellationToken);

            // Assert
            if (!testCase.ShouldBeValid)
            {
                this.loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("provided invalid donation amount")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce);
            }
        }
    }
}