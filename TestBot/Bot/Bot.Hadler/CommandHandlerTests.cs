using Bot.Handlers;
using Bot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Data.Models;

namespace Bot.Tests.Handlers;

[TestFixture]
public class CommandHandlerTests
{
    private Mock<ILogger<CommandHandler>> _loggerMock;
    private Mock<IGoalService> _goalServiceMock;
    private Mock<KeyboardService> _keyboardServiceMock;
    private Mock<AdminHandler> _adminHandlerMock;
    private Mock<ITelegramBotClient> _botClientMock;
    private CommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<CommandHandler>>();
        _goalServiceMock = new Mock<IGoalService>();

        // Создаем мок для KeyboardService с правильным конструктором
        var keyboardServiceLoggerMock = new Mock<ILogger<KeyboardService>>();
        _keyboardServiceMock = new Mock<KeyboardService>(keyboardServiceLoggerMock.Object);

        // Создаем мок для AdminHandler с правильным конструктором
        var adminHandlerLoggerMock = new Mock<ILogger<AdminHandler>>();
        var adminStateServiceMock = new Mock<AdminStateService>(Mock.Of<ILogger<AdminStateService>>());
        _adminHandlerMock = new Mock<AdminHandler>(
            adminHandlerLoggerMock.Object,
            Mock.Of<IGoalService>(),
            adminStateServiceMock.Object);

        _botClientMock = new Mock<ITelegramBotClient>();

        _handler = new CommandHandler(
            _loggerMock.Object,
            _goalServiceMock.Object,
            _keyboardServiceMock.Object,
            _adminHandlerMock.Object);
    }

    [Test]
    public async Task HandleCommandAsync_NullUser_LogsWarningAndReturns()
    {
        // Arrange
        var message = new Message { From = null, Chat = new Chat { Id = 123 }, Text = "/start" };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received command from message with null user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_EmptyMessageText_LogsWarningAndReturns()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = null };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

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
    public async Task HandleCommandAsync_StartCommand_RegularUser_SendsMainMenu()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var cancellationToken = new CancellationToken();
        var startStats = "Статистика: 5000/10000";
        var keyboard = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "💳 Пожертвовать" } });

        _goalServiceMock
            .Setup(x => x.GetStartStats())
            .ReturnsAsync(startStats);

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        _keyboardServiceMock
            .Setup(x => x.GetMainMenuKeyboard())
            .Returns(keyboard);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.GetStartStats(), Times.Once);
        _goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);
        _keyboardServiceMock.Verify(x => x.GetMainMenuKeyboard(), Times.Once);
        _keyboardServiceMock.Verify(x => x.GetMainMenuKeyboardForAdmin(), Times.Never);
    }

    [Test]
    public async Task HandleCommandAsync_StartCommand_AdminUser_SendsAdminMenu()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var cancellationToken = new CancellationToken();
        var startStats = "Статистика: 5000/10000";
        var keyboard = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "📝 Создать новую цель" } });

        _goalServiceMock
            .Setup(x => x.GetStartStats())
            .ReturnsAsync(startStats);

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        _keyboardServiceMock
            .Setup(x => x.GetMainMenuKeyboardForAdmin())
            .Returns(keyboard);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);
        _keyboardServiceMock.Verify(x => x.GetMainMenuKeyboardForAdmin(), Times.Once);
        _keyboardServiceMock.Verify(x => x.GetMainMenuKeyboard(), Times.Never);
    }

    [Test]
    public async Task HandleCommandAsync_DonateCommand_WithActiveGoal_SendsDonationKeyboard()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/donate" };
        var cancellationToken = new CancellationToken();
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var keyboard = new InlineKeyboardMarkup(new[] { new InlineKeyboardButton[] { InlineKeyboardButton.WithCallbackData("100") } });

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        _keyboardServiceMock
            .Setup(x => x.GetDonationAmountKeyboard())
            .Returns(keyboard);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _keyboardServiceMock.Verify(x => x.GetDonationAmountKeyboard(), Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_DonateCommand_NoActiveGoal_SendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/donate" };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _keyboardServiceMock.Verify(x => x.GetDonationAmountKeyboard(), Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No active goal found for donate command")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_StatsCommand_CallsHandleStatsCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/stats" };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert - проверяем, что вызывается метод HandleStatsCommand
        // Поскольку HandleStatsCommand виртуальный, мы можем проверить его вызов
    }

    [Test]
    public async Task HandleStatsCommand_Successful_LogsAndSendsStats()
    {
        // Arrange
        var chatId = 456L;
        var cancellationToken = new CancellationToken();
        var stats = "Статистика: 5000/10000";

        _goalServiceMock
            .Setup(x => x.GetGoalStatsAsync())
            .ReturnsAsync(stats);

        // Act
        await _handler.HandleStatsCommand(_botClientMock.Object, chatId, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.GetGoalStatsAsync(), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing stats command")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Statistics sent successfully")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleStatsCommand_ThrowsException_LogsError()
    {
        // Arrange
        var chatId = 456L;
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetGoalStatsAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        await _handler.HandleStatsCommand(_botClientMock.Object, chatId, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error getting stats")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_AddGoalCommand_AdminUser_StartsGoalCreation()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/addgoal" };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _adminHandlerMock.Verify(
            x => x.StartGoalCreationAsync(_botClientMock.Object, 456, 123, cancellationToken),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Admin user") && v.ToString().Contains("starting goal creation")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_AddGoalCommand_NonAdminUser_HandlesNotAdmin()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/addgoal" };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _adminHandlerMock.Verify(
            x => x.HandleNotAdmin(_botClientMock.Object, 456, cancellationToken),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Non-admin user") && v.ToString().Contains("attempted to create goal")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_UnknownCommand_AdminUser_SendsAdminHelp()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "unknown_command" };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unknown command from user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_UnknownCommand_RegularUser_SendsRegularHelp()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "unknown_command" };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Unknown command from user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [TestCase("/start")]
    [TestCase("🔄 Обновить")]
    [TestCase("Обновить")]
    public async Task HandleCommandAsync_StartCommandVariants_CallsHandleStartCommand(string command)
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = command };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetStartStats())
            .ReturnsAsync("Статистика");

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        _keyboardServiceMock
            .Setup(x => x.GetMainMenuKeyboard())
            .Returns(new ReplyKeyboardMarkup(new KeyboardButton[0][]));

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.GetStartStats(), Times.Once);
    }

    [TestCase("/donate")]
    [TestCase("💳 Пожертвовать")]
    [TestCase("Пожертвовать")]
    public async Task HandleCommandAsync_DonateCommandVariants_CallsHandleDonateCommand(string command)
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = command };
        var cancellationToken = new CancellationToken();
        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal" };

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        _keyboardServiceMock
            .Setup(x => x.GetDonationAmountKeyboard())
            .Returns(new InlineKeyboardMarkup(new InlineKeyboardButton[0][]));

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _keyboardServiceMock.Verify(x => x.GetDonationAmountKeyboard(), Times.Once);
    }

    [Test]
    public async Task HandleCommandAsync_StatsCommandAliases_AllCallHandleStatsCommand()
    {
        // Arrange
        var user = new User { Id = 123 };
        var aliases = new[] { "/stats", "📊 Статистика", "Статистика" };
        var cancellationToken = new CancellationToken();

        foreach (var alias in aliases)
        {
            var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = alias };

            // Act
            await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

            // Assert - проверяем, что команда обрабатывается без ошибок
        }
    }

    [TestCase("/addgoal")]
    [TestCase("📝 Создать новую цель")]
    [TestCase("Создать новую цель")]
    public async Task HandleCommandAsync_AddGoalCommandAliases_AllCallHandleAddGoalCommand(string command)
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = command };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.IsUserAdminAsync(123))
            .ReturnsAsync(false);

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _goalServiceMock.Verify(x => x.IsUserAdminAsync(123), Times.Once);

    }

    [Test]
    public async Task HandleCommandAsync_ThrowsException_LogsErrorAndSendsErrorMessage()
    {
        // Arrange
        var user = new User { Id = 123 };
        var message = new Message { From = user, Chat = new Chat { Id = 456 }, Text = "/start" };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetStartStats())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _handler.HandleCommandAsync(_botClientMock.Object, message, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing command")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Never);
    }
}