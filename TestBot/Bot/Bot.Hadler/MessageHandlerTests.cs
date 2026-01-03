// <copyright file="MessageHandlerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

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

/// <summary>
/// Unit tests for the <see cref="MessageHandler"/> class.
/// </summary>
[TestFixture]
public class MessageHandlerTests
{
    private Mock<ILogger<MessageHandler>> loggerMock;
    private Mock<IDonationService> donationServiceMock;
    private Mock<IGoalService> goalServiceMock;
    private Mock<CommandHandler> commandHandlerMock;
    private Mock<PaymentHandler> paymentHandlerMock;
    private Mock<UserStateService> userStateServiceMock;
    private Mock<AdminHandler> adminHandlerMock;
    private Mock<AdminStateService> adminStateServiceMock;
    private Mock<ITelegramBotClient> botClientMock;
    private MessageHandler handler;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this.loggerMock = new Mock<ILogger<MessageHandler>>();
        this.donationServiceMock = new Mock<IDonationService>();
        this.goalServiceMock = new Mock<IGoalService>();

        // Create mocks for all dependencies explicitly, as in the previous Setup

        // Create mock logger for UserStateService
        var userStateServiceLoggerMock = new Mock<ILogger<UserStateService>>();
        this.userStateServiceMock = new Mock<UserStateService>(userStateServiceLoggerMock.Object);

        // Create mocks for PaymentHandler dependencies (as in previous Setup)
        var paymentLoggerMock = new Mock<ILogger<PaymentHandler>>();
        var paymentGoalServiceMock = new Mock<IGoalService>();
        var paymentDonationServiceMock = new Mock<IDonationService>();
        var userStateServiceForPaymentMock = new Mock<UserStateService>(Mock.Of<ILogger<UserStateService>>());
        var botConfigMock = new Mock<IOptions<BotConfig>>();
        botConfigMock.Setup(x => x.Value).Returns(new BotConfig
        {
            PaymentProviderToken = "test-token",
        });

        this.paymentHandlerMock = new Mock<PaymentHandler>(
            paymentLoggerMock.Object,
            paymentGoalServiceMock.Object,
            paymentDonationServiceMock.Object,
            userStateServiceForPaymentMock.Object,
            botConfigMock.Object);

        // Create mocks for CommandHandler dependencies
        var commandLoggerMock = new Mock<ILogger<CommandHandler>>();
        var commandGoalServiceMock = new Mock<IGoalService>();
        var keyboardServiceMock = new Mock<KeyboardService>(Mock.Of<ILogger<KeyboardService>>());

        // Create mock for AdminStateService
        var adminStateServiceLoggerMock = new Mock<ILogger<AdminStateService>>();
        this.adminStateServiceMock = new Mock<AdminStateService>(adminStateServiceLoggerMock.Object);

        // Create mock for AdminHandler
        this.adminHandlerMock = new Mock<AdminHandler>(
            Mock.Of<ILogger<AdminHandler>>(),
            Mock.Of<IGoalService>(),
            this.adminStateServiceMock.Object);

        this.commandHandlerMock = new Mock<CommandHandler>(
            commandLoggerMock.Object,
            commandGoalServiceMock.Object,
            keyboardServiceMock.Object,
            this.adminHandlerMock.Object);

        this.botClientMock = new Mock<ITelegramBotClient>();

        // Configure base methods
        this.donationServiceMock
            .Setup(x => x.GetOrCreateUserAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.FromResult(new Data.Models.Users
            {
                Id = 123,
                Username = "testuser",
                FirstName = "Test",
                LastName = "User",
            })); // Fixed - returning User

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(It.IsAny<long>()))
            .ReturnsAsync(false);

        this.commandHandlerMock
            .Setup(x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.paymentHandlerMock
            .Setup(x => x.HandleSuccessfulPaymentAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.paymentHandlerMock
            .Setup(x => x.HandleCustomAmountInputAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.adminHandlerMock
            .Setup(x => x.HandleAdminGoalCreationAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.handler = new MessageHandler(
            this.loggerMock.Object,
            this.donationServiceMock.Object,
            this.goalServiceMock.Object,
            this.commandHandlerMock.Object,
            this.paymentHandlerMock.Object,
            this.userStateServiceMock.Object,
            this.adminHandlerMock.Object,
            this.adminStateServiceMock.Object);
    }

    /// <summary>
    /// Tests that the handler correctly identifies updates with messages.
    /// </summary>
    [Test]
    public void CanHandleWithMessageReturnsTrue()
    {
        // Arrange
        var update = new Update { Message = new Message() };

        // Act
        var result = this.handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests that the handler correctly identifies updates without messages.
    /// </summary>
    [Test]
    public void CanHandleWithoutMessageReturnsFalse()
    {
        // Arrange
        var update = new Update { Message = null };

        // Act
        var result = this.handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests that a message with a null user triggers a warning log and returns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncMessageWithNullUserLogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 } };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received message with null user information")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify that business logic is not called
        this.donationServiceMock.Verify(
            x => x.GetOrCreateUserAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that a valid message triggers user registration or update.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncValidMessageRegistersOrUpdatesUser()
    {
        // Arrange
        var user = new User { Id = 123, Username = "testuser", FirstName = "Test", LastName = "User" };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "test" };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.donationServiceMock.Verify(
            x => x.GetOrCreateUserAsync(123, "testuser", "Test", "User"),
            Times.Once);
    }

    /// <summary>
    /// Tests that a message with successful payment triggers payment processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncSuccessfulPaymentProcessesPayment()
    {
        // Arrange
        var user = new User { Id = 123 };
        var successfulPayment = new SuccessfulPayment();
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, SuccessfulPayment = successfulPayment };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.paymentHandlerMock.Verify(
            x => x.HandleSuccessfulPaymentAsync(this.botClientMock.Object, message, cancellationToken),
            Times.Once);

        // Verify that further processing does not occur
        this.commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that a message from a user waiting for amount triggers custom amount processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncUserWaitingForAmountProcessesCustomAmount()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "500" };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        this.userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(true);

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.paymentHandlerMock.Verify(
            x => x.HandleCustomAmountInputAsync(this.botClientMock.Object, message, cancellationToken),
            Times.Once);

        // Verify that regular command processing does not occur
        this.commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that a message from an admin creating a goal triggers goal creation processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncAdminCreatingGoalProcessesGoalCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Новая цель" };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        this.userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        this.adminStateServiceMock
            .Setup(x => x.IsUserCreatingGoal(123))
            .Returns(true);

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.adminHandlerMock.Verify(
            x => x.HandleAdminGoalCreationAsync(this.botClientMock.Object, message, cancellationToken),
            Times.Once);

        // Verify that regular command processing does not occur
        this.commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that a regular text message triggers command processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncRegularTextMessageProcessesCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        this.userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.commandHandlerMock.Verify(
            x => x.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken),
            Times.Once);
    }

    /// <summary>
    /// Tests that a non-text message does not trigger command processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncNonTextMessageDoesNotProcessCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null }; // Non-text message
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        this.userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.commandHandlerMock.Verify(
            x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Tests that when user registration fails, error is logged but processing continues.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncUserRegistrationFailsLogsErrorButContinues()
    {
        // Arrange
        var user = new User { Id = 123, Username = "testuser", FirstName = "Test", LastName = "User" };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        this.donationServiceMock
            .Setup(x => x.GetOrCreateUserAsync(123, "testuser", "Test", "User"))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Failed to register/update user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify that command processing still occurs
        this.commandHandlerMock.Verify(
            x => x.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken),
            Times.Once);
    }

    /// <summary>
    /// Tests that when command handler throws an exception, error is logged.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncCommandHandlerThrowsExceptionLogsError()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        this.userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        this.commandHandlerMock
            .Setup(x => x.HandleCommandAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Command processing failed"));

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error processing text message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when an admin is not creating a goal, message is processed as regular command.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncAdminNotCreatingGoalProcessesAsRegularCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var update = new Update { Message = message };
        var cancellationToken = CancellationToken.None;

        this.userStateServiceMock
            .Setup(x => x.IsWaitingForAmount(123, 456))
            .Returns(false);

        this.goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true); // User is admin

        this.adminStateServiceMock
            .Setup(x => x.IsUserCreatingGoal(123))
            .Returns(false); // But not creating a goal

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.adminHandlerMock.Verify(
            x => x.HandleAdminGoalCreationAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Message>(), It.IsAny<CancellationToken>()),
            Times.Never);

        this.commandHandlerMock.Verify(
            x => x.HandleCommandAsync(this.botClientMock.Object, message, cancellationToken),
            Times.Once);
    }
}