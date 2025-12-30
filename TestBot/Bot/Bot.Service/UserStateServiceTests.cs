using Bot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Bot.Tests.Services;

[TestFixture]
public class UserStateServiceTests
{
    private Mock<ILogger<UserStateService>> _loggerMock;
    private UserStateService _userStateService;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<UserStateService>>();
        _userStateService = new UserStateService(_loggerMock.Object);
    }

    [Test]
    public void SetWaitingForAmount_NewUser_SetsState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;

        // Act
        _userStateService.SetWaitingForAmount(userId, chatId);

        // Assert
        var isWaiting = _userStateService.IsWaitingForAmount(userId, chatId);
        Assert.That(isWaiting, Is.True);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("User") && v.ToString().Contains("set to waiting for amount input")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void SetWaitingForAmount_ExistingUser_UpdatesState()
    {
        // Arrange
        var userId = 123L;
        _userStateService.SetWaitingForAmount(userId, 456L);

        // Act - обновляем chatId для того же пользователя
        _userStateService.SetWaitingForAmount(userId, 789L);

        // Assert
        var isWaitingOldChat = _userStateService.IsWaitingForAmount(userId, 456L);
        var isWaitingNewChat = _userStateService.IsWaitingForAmount(userId, 789L);

        Assert.That(isWaitingOldChat, Is.False);
        Assert.That(isWaitingNewChat, Is.True);
    }

    [Test]
    public void IsWaitingForAmount_UserNotWaiting_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;

        // Act
        var isWaiting = _userStateService.IsWaitingForAmount(userId, chatId);

        // Assert
        Assert.That(isWaiting, Is.False);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Checked waiting for amount status") && v.ToString().Contains("False")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void IsWaitingForAmount_UserWaitingInDifferentChat_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        _userStateService.SetWaitingForAmount(userId, 456L);

        // Act
        var isWaiting = _userStateService.IsWaitingForAmount(userId, 789L); // Другой chatId

        // Assert
        Assert.That(isWaiting, Is.False);
    }

    [Test]
    public void IsWaitingForAmount_UserWaitingInSameChat_ReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _userStateService.SetWaitingForAmount(userId, chatId);

        // Act
        var isWaiting = _userStateService.IsWaitingForAmount(userId, chatId);

        // Assert
        Assert.That(isWaiting, Is.True);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Checked waiting for amount status") && v.ToString().Contains("True")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void RemoveWaitingForAmount_ExistingUser_RemovesState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        _userStateService.SetWaitingForAmount(userId, chatId);

        // Act
        _userStateService.RemoveWaitingForAmount(userId);

        // Assert
        var isWaiting = _userStateService.IsWaitingForAmount(userId, chatId);
        Assert.That(isWaiting, Is.False);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Removed user") && v.ToString().Contains("from waiting for amount state")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void RemoveWaitingForAmount_NonExistentUser_LogsDebug()
    {
        // Arrange
        var userId = 999L; // Несуществующий пользователь

        // Act
        _userStateService.RemoveWaitingForAmount(userId);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Attempted to remove non-existent waiting state for user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void GetWaitingUsersCount_NoUsers_ReturnsZero()
    {
        // Arrange - нет пользователей в состоянии ожидания

        // Act
        var count = _userStateService.GetWaitingUsersCount();

        // Assert
        Assert.That(count, Is.EqualTo(0));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Current users waiting for amount input: 0")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void GetWaitingUsersCount_MultipleUsers_ReturnsCorrectCount()
    {
        // Arrange
        _userStateService.SetWaitingForAmount(123L, 456L);
        _userStateService.SetWaitingForAmount(124L, 457L);
        _userStateService.SetWaitingForAmount(125L, 458L);

        // Act
        var count = _userStateService.GetWaitingUsersCount();

        // Assert
        Assert.That(count, Is.EqualTo(3));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Current users waiting for amount input: 3")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void GetWaitingUsersCount_AfterRemoval_ReturnsUpdatedCount()
    {
        // Arrange
        _userStateService.SetWaitingForAmount(123L, 456L);
        _userStateService.SetWaitingForAmount(124L, 457L);
        _userStateService.SetWaitingForAmount(125L, 458L);

        // Act - удаляем одного пользователя
        _userStateService.RemoveWaitingForAmount(124L);
        var count = _userStateService.GetWaitingUsersCount();

        // Assert
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void ClearAllWaitingStates_WithUsers_ClearsAllStates()
    {
        // Arrange
        _userStateService.SetWaitingForAmount(123L, 456L);
        _userStateService.SetWaitingForAmount(124L, 457L);
        _userStateService.SetWaitingForAmount(125L, 458L);

        // Act
        _userStateService.ClearAllWaitingStates();

        // Assert
        var count = _userStateService.GetWaitingUsersCount();
        Assert.That(count, Is.EqualTo(0));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cleared all waiting states, affected 3 users")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void ClearAllWaitingStates_NoUsers_LogsZero()
    {
        // Arrange - нет пользователей

        // Act
        _userStateService.ClearAllWaitingStates();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cleared all waiting states, affected 0 users")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void RemoveMultipleWaitingStates_AllUsersExist_RemovesAll()
    {
        // Arrange
        _userStateService.SetWaitingForAmount(123L, 456L);
        _userStateService.SetWaitingForAmount(124L, 457L);
        _userStateService.SetWaitingForAmount(125L, 458L);

        var userIdsToRemove = new long[] { 123L, 124L, 125L };

        // Act
        var removedCount = _userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(3));

        var remainingCount = _userStateService.GetWaitingUsersCount();
        Assert.That(remainingCount, Is.EqualTo(0));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Removed waiting states for 3 out of 3 users")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void RemoveMultipleWaitingStates_SomeUsersExist_RemovesOnlyExisting()
    {
        // Arrange
        _userStateService.SetWaitingForAmount(123L, 456L);
        _userStateService.SetWaitingForAmount(124L, 457L);
        // 125L не добавляли

        var userIdsToRemove = new long[] { 123L, 124L, 125L };

        // Act
        var removedCount = _userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(2));

        var remainingCount = _userStateService.GetWaitingUsersCount();
        Assert.That(remainingCount, Is.EqualTo(0));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Removed waiting states for 2 out of 3 users")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void RemoveMultipleWaitingStates_NoUsersExist_ReturnsZero()
    {
        // Arrange
        var userIdsToRemove = new long[] { 123L, 124L, 125L };

        // Act
        var removedCount = _userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(0));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Removed waiting states for 0 out of 3 users")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void RemoveMultipleWaitingStates_EmptyList_ReturnsZero()
    {
        // Arrange
        var userIdsToRemove = Array.Empty<long>();

        // Act
        var removedCount = _userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(0));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Removed waiting states for 0 out of 0 users")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void MultipleOperations_ComplexScenario_WorksCorrectly()
    {
        // Arrange & Act - комплексный сценарий
        _userStateService.SetWaitingForAmount(123L, 456L);
        _userStateService.SetWaitingForAmount(124L, 457L);

        var countAfterAdd = _userStateService.GetWaitingUsersCount();
        Assert.That(countAfterAdd, Is.EqualTo(2));

        // Проверяем состояния
        var user123Waiting = _userStateService.IsWaitingForAmount(123L, 456L);
        var user124Waiting = _userStateService.IsWaitingForAmount(124L, 457L);
        Assert.That(user123Waiting, Is.True);
        Assert.That(user124Waiting, Is.True);

        // Обновляем состояние одного пользователя
        _userStateService.SetWaitingForAmount(123L, 789L);

        var user123OldChat = _userStateService.IsWaitingForAmount(123L, 456L);
        var user123NewChat = _userStateService.IsWaitingForAmount(123L, 789L);
        Assert.That(user123OldChat, Is.False);
        Assert.That(user123NewChat, Is.True);

        // Удаляем одного пользователя
        _userStateService.RemoveWaitingForAmount(124L);

        var countAfterRemove = _userStateService.GetWaitingUsersCount();
        Assert.That(countAfterRemove, Is.EqualTo(1));

        // Очищаем все
        _userStateService.ClearAllWaitingStates();

        var finalCount = _userStateService.GetWaitingUsersCount();
        Assert.That(finalCount, Is.EqualTo(0));
    }

    [Test]
    public void IndependentUsers_DoNotInterfere()
    {
        // Arrange
        var user1 = 123L;
        var user2 = 124L;
        var chat1 = 456L;
        var chat2 = 457L;

        // Act
        _userStateService.SetWaitingForAmount(user1, chat1);
        _userStateService.SetWaitingForAmount(user2, chat2);

        // Assert - проверяем, что состояния независимы
        var user1InChat1 = _userStateService.IsWaitingForAmount(user1, chat1);
        var user1InChat2 = _userStateService.IsWaitingForAmount(user1, chat2);
        var user2InChat1 = _userStateService.IsWaitingForAmount(user2, chat1);
        var user2InChat2 = _userStateService.IsWaitingForAmount(user2, chat2);

        Assert.That(user1InChat1, Is.True);
        Assert.That(user1InChat2, Is.False);
        Assert.That(user2InChat1, Is.False);
        Assert.That(user2InChat2, Is.True);

        // Удаляем только user1
        _userStateService.RemoveWaitingForAmount(user1);

        var user1AfterRemove = _userStateService.IsWaitingForAmount(user1, chat1);
        var user2AfterRemove = _userStateService.IsWaitingForAmount(user2, chat2);

        Assert.That(user1AfterRemove, Is.False);
        Assert.That(user2AfterRemove, Is.True);
    }
}