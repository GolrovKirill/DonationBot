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

[TestFixture]
public class AdminHandlerTests
{
    private Mock<ILogger<AdminHandler>> _loggerMock;
    private Mock<IGoalService> _goalServiceMock;
    private Mock<AdminStateService> _adminStateServiceMock;
    private Mock<ITelegramBotClient> _botClientMock;
    private AdminHandler _handler;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<AdminHandler>>();
        _goalServiceMock = new Mock<IGoalService>();

        // Создаем мок для AdminStateService с правильным конструктором
        var adminStateServiceLoggerMock = new Mock<ILogger<AdminStateService>>();
        _adminStateServiceMock = new Mock<AdminStateService>(adminStateServiceLoggerMock.Object);

        _botClientMock = new Mock<ITelegramBotClient>();

        _handler = new AdminHandler(
            _loggerMock.Object,
            _goalServiceMock.Object,
            _adminStateServiceMock.Object);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_NullUser_LogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, Text = "Test" };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received message with null user ID")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_NullState_LogsWarningAndReturns()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Test" };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns((AdminGoalCreationState)null);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No admin state found for user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_EmptyMessageText_LogsWarningAndReturns()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForTitle };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received empty message text")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForTitle_ValidTitle_ProcessesTitleStep()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Новая цель" };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForTitle };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _adminStateServiceMock.Verify(
            x => x.SetTitle(123, "Новая цель"),
            Times.Once);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForTitle_TooLongTitle_CancelsCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var longTitle = new string('a', 256); // 256 символов > 255
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = longTitle };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForTitle };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("provided title that exceeds length limit")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForDescription_ProcessesDescriptionStep()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Описание цели" };
        var state = new AdminGoalCreationState { CurrentStep = AdminGoalStep.WaitingForDescription };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _adminStateServiceMock.Verify(
            x => x.SetDescription(123, "Описание цели"),
            Times.Once);
    }

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
            Description = "Test Description"
        };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("provided invalid amount")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_WaitingForAmount_AmountTooLarge_CancelsCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "100000000" }; // 100 миллионов
        var state = new AdminGoalCreationState
        {
            CurrentStep = AdminGoalStep.WaitingForAmount,
            Title = "Test Goal",
            Description = "Test Description"
        };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);
    }

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
            Description = "Test Description"
        };
        var cancellationToken = new CancellationToken();

        var createdGoal = new Data.Models.DonationGoal { Id = 1, Title = "Test Goal", Description = "Test Description", TargetAmount = 5000 };
        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        _goalServiceMock
            .Setup(x => x.CreateGoalAsync("Test Goal", "Test Description", 5000))
            .ReturnsAsync(createdGoal);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(
            x => x.CreateGoalAsync("Test Goal", "Test Description", 5000),
            Times.Once);

        _adminStateServiceMock.Verify(
            x => x.CancelGoalCreation(123),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Goal created successfully")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

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
            Description = "Test Description"
        };
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        _goalServiceMock
            .Setup(x => x.CreateGoalAsync("Test Goal", "Test Description", 5000))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to create goal in database")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task StartGoalCreationAsync_SetsStateAndLogs()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.StartGoalCreationAsync(_botClientMock.Object, chatId, userId, cancellationToken);

        // Assert
        _adminStateServiceMock.Verify(
            x => x.StartGoalCreation(123, 456),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting goal creation process")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task StartGoalCreationAsync_ThrowsException_LogsError()
    {
        // Arrange
        var chatId = 456L;
        var userId = 123L;
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.StartGoalCreation(123, 456))
            .Throws(new Exception("State service error"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() =>
            _handler.StartGoalCreationAsync(_botClientMock.Object, chatId, userId, cancellationToken));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to start goal creation")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleNotAdmin_LogsWarning()
    {
        // Arrange
        var chatId = 456L;
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleNotAdmin(_botClientMock.Object, chatId, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Non-admin access attempt detected")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAdminGoalCreationAsync_UnknownStep_LogsWarning()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "Test" };
        var state = new AdminGoalCreationState { CurrentStep = (AdminGoalStep)999 }; // Неизвестный шаг
        var cancellationToken = new CancellationToken();

        _adminStateServiceMock
            .Setup(x => x.GetState(123))
            .Returns(state);

        // Act
        await _handler.HandleAdminGoalCreationAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unknown admin step")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }
}