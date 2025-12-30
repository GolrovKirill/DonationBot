using Bot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using static Bot.Services.AdminStateService;

namespace Bot.Tests.Services;

[TestFixture]
public class AdminStateServiceTests
{
    private Mock<ILogger<AdminStateService>> _loggerMock;
    private AdminStateService _adminStateService;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<AdminStateService>>();
        _adminStateService = new AdminStateService(_loggerMock.Object);
    }

    [Test]
    public void StartGoalCreation_NewUser_SetsInitialState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;

        // Act
        _adminStateService.StartGoalCreation(userId, chatId);

        // Assert
        var state = _adminStateService.GetState(userId);
        Assert.That(state, Is.Not.Null);
        Assert.That(state.ChatId, Is.EqualTo(chatId));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForTitle));
        Assert.That(state.Title, Is.Null);
        Assert.That(state.Description, Is.Null);
        Assert.That(state.TargetAmount, Is.Null);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Started goal creation for admin user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void StartGoalCreation_ExistingUser_OverwritesState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _adminStateService.StartGoalCreation(userId, chatId);

        // Act - запускаем создание цели заново
        _adminStateService.StartGoalCreation(userId, 789L); // Новый chatId

        // Assert
        var state = _adminStateService.GetState(userId);
        Assert.That(state.ChatId, Is.EqualTo(789L));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForTitle));
    }

    [Test]
    public void GetState_NonExistentUser_ReturnsNull()
    {
        // Arrange
        var userId = 999L; // Несуществующий пользователь

        // Act
        var state = _adminStateService.GetState(userId);

        // Assert
        Assert.That(state, Is.Null);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No state found for user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void GetState_ExistingUser_ReturnsState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _adminStateService.StartGoalCreation(userId, chatId);

        // Act
        var state = _adminStateService.GetState(userId);

        // Assert
        Assert.That(state, Is.Not.Null);
        Assert.That(state.ChatId, Is.EqualTo(chatId));
    }

    [Test]
    public void SetTitle_ExistingUser_SetsTitleAndAdvancesStep()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        var title = "Новая цель";
        _adminStateService.StartGoalCreation(userId, chatId);

        // Act
        _adminStateService.SetTitle(userId, title);

        // Assert
        var state = _adminStateService.GetState(userId);
        Assert.That(state.Title, Is.EqualTo(title));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForDescription));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Set title for user") && v.ToString().Contains(title)),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void SetTitle_NonExistentUser_LogsWarning()
    {
        // Arrange
        var userId = 999L; // Несуществующий пользователь
        var title = "Новая цель";

        // Act
        _adminStateService.SetTitle(userId, title);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempted to set title for non-existent user state")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void SetDescription_ExistingUser_SetsDescriptionAndAdvancesStep()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        var description = "Описание цели";
        _adminStateService.StartGoalCreation(userId, chatId);
        _adminStateService.SetTitle(userId, "Название");

        // Act
        _adminStateService.SetDescription(userId, description);

        // Assert
        var state = _adminStateService.GetState(userId);
        Assert.That(state.Description, Is.EqualTo(description));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForAmount));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Set description for user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void SetDescription_NonExistentUser_LogsWarning()
    {
        // Arrange
        var userId = 999L; // Несуществующий пользователь
        var description = "Описание цели";

        // Act
        _adminStateService.SetDescription(userId, description);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempted to set description for non-existent user state")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void SetAmount_ExistingUser_SetsAmountAndCompletesProcess()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        var amount = 5000m;
        _adminStateService.StartGoalCreation(userId, chatId);
        _adminStateService.SetTitle(userId, "Название");
        _adminStateService.SetDescription(userId, "Описание");

        // Act
        _adminStateService.SetAmount(userId, amount);

        // Assert
        var state = _adminStateService.GetState(userId);
        Assert.That(state.TargetAmount, Is.EqualTo(amount));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.None));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Set amount for user") && v.ToString().Contains(amount.ToString())),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void SetAmount_NonExistentUser_LogsWarning()
    {
        // Arrange
        var userId = 999L; // Несуществующий пользователь
        var amount = 5000m;

        // Act
        _adminStateService.SetAmount(userId, amount);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempted to set amount for non-existent user state")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void CancelGoalCreation_ExistingUser_RemovesState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _adminStateService.StartGoalCreation(userId, chatId);

        // Act
        _adminStateService.CancelGoalCreation(userId);

        // Assert
        var state = _adminStateService.GetState(userId);
        Assert.That(state, Is.Null);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Canceled goal creation for user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void CancelGoalCreation_NonExistentUser_LogsDebug()
    {
        // Arrange
        var userId = 999L; // Несуществующий пользователь

        // Act
        _adminStateService.CancelGoalCreation(userId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempted to cancel non-existent goal creation for user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void IsUserCreatingGoal_UserInProcess_ReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _adminStateService.StartGoalCreation(userId, chatId);

        // Act
        var isCreating = _adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.True);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User") && v.ToString().Contains("goal creation status: True")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void IsUserCreatingGoal_UserNotInProcess_ReturnsFalse()
    {
        // Arrange
        var userId = 123L; // Пользователь не начинал создание цели

        // Act
        var isCreating = _adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.False);
    }

    [Test]
    public void IsUserCreatingGoal_UserCompletedProcess_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _adminStateService.StartGoalCreation(userId, chatId);
        _adminStateService.SetTitle(userId, "Название");
        _adminStateService.SetDescription(userId, "Описание");
        _adminStateService.SetAmount(userId, 5000m); // Завершаем процесс

        // Act
        var isCreating = _adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.False);
    }

    [Test]
    public void IsUserCreatingGoal_UserCancelledProcess_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _adminStateService.StartGoalCreation(userId, chatId);
        _adminStateService.CancelGoalCreation(userId); // Отменяем процесс

        // Act
        var isCreating = _adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.False);
    }

    [Test]
    public void GetActiveStateCount_NoUsers_ReturnsZero()
    {
        // Arrange - нет активных пользователей

        // Act
        var count = _adminStateService.GetActiveStateCount();

        // Assert
        Assert.That(count, Is.EqualTo(0));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Current active admin states: 0")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void GetActiveStateCount_MultipleUsers_ReturnsCorrectCount()
    {
        // Arrange
        _adminStateService.StartGoalCreation(123L, 456L);
        _adminStateService.StartGoalCreation(124L, 457L);
        _adminStateService.StartGoalCreation(125L, 458L);

        // Act
        var count = _adminStateService.GetActiveStateCount();

        // Assert
        Assert.That(count, Is.EqualTo(3));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Current active admin states: 3")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void GetActiveStateCount_AfterCancellation_ReturnsUpdatedCount()
    {
        // Arrange
        _adminStateService.StartGoalCreation(123L, 456L);
        _adminStateService.StartGoalCreation(124L, 457L);
        _adminStateService.StartGoalCreation(125L, 458L);

        // Act - отменяем одного пользователя
        _adminStateService.CancelGoalCreation(124L);
        var count = _adminStateService.GetActiveStateCount();

        // Assert
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void MultipleUsers_IndependentStates()
    {
        // Arrange
        var user1 = 123L;
        var user2 = 124L;
        var chat1 = 456L;
        var chat2 = 457L;

        // Act
        _adminStateService.StartGoalCreation(user1, chat1);
        _adminStateService.StartGoalCreation(user2, chat2);

        _adminStateService.SetTitle(user1, "Цель пользователя 1");
        _adminStateService.SetTitle(user2, "Цель пользователя 2");

        // Assert
        var state1 = _adminStateService.GetState(user1);
        var state2 = _adminStateService.GetState(user2);

        Assert.That(state1.Title, Is.EqualTo("Цель пользователя 1"));
        Assert.That(state2.Title, Is.EqualTo("Цель пользователя 2"));
        Assert.That(state1.ChatId, Is.EqualTo(chat1));
        Assert.That(state2.ChatId, Is.EqualTo(chat2));
    }
}