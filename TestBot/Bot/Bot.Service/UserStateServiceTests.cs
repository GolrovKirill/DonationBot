// <copyright file="UserStateServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Bot.Tests.Services;

/// <summary>
/// Unit tests for the <see cref="UserStateService"/> class.
/// </summary>
[TestFixture]
public class UserStateServiceTests
{
    private Mock<ILogger<UserStateService>> loggerMock;
    private UserStateService userStateService;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this.loggerMock = new Mock<ILogger<UserStateService>>();
        this.userStateService = new UserStateService(this.loggerMock.Object);
    }

    /// <summary>
    /// Tests that setting waiting for amount for a new user sets the state correctly.
    /// </summary>
    [Test]
    public void SetWaitingForAmountNewUserSetsState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;

        // Act
        this.userStateService.SetWaitingForAmount(userId, chatId);

        // Assert
        var isWaiting = this.userStateService.IsWaitingForAmount(userId, chatId);
        Assert.That(isWaiting, Is.True);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("User") && v.ToString() !.Contains("set to waiting for amount input")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that setting waiting for amount for an existing user updates the state.
    /// </summary>
    [Test]
    public void SetWaitingForAmountExistingUserUpdatesState()
    {
        // Arrange
        var userId = 123L;
        this.userStateService.SetWaitingForAmount(userId, 456L);

        // Act - update chatId for the same user
        this.userStateService.SetWaitingForAmount(userId, 789L);

        // Assert
        var isWaitingOldChat = this.userStateService.IsWaitingForAmount(userId, 456L);
        var isWaitingNewChat = this.userStateService.IsWaitingForAmount(userId, 789L);

        Assert.That(isWaitingOldChat, Is.False);
        Assert.That(isWaitingNewChat, Is.True);
    }

    /// <summary>
    /// Tests that checking waiting for amount for a user not waiting returns false.
    /// </summary>
    [Test]
    public void IsWaitingForAmountUserNotWaitingReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;

        // Act
        var isWaiting = this.userStateService.IsWaitingForAmount(userId, chatId);

        // Assert
        Assert.That(isWaiting, Is.False);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Checked waiting for amount status") && v.ToString() !.Contains("False")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that checking waiting for amount for a user waiting in a different chat returns false.
    /// </summary>
    [Test]
    public void IsWaitingForAmountUserWaitingInDifferentChatReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        this.userStateService.SetWaitingForAmount(userId, 456L);

        // Act
        var isWaiting = this.userStateService.IsWaitingForAmount(userId, 789L); // Different chatId

        // Assert
        Assert.That(isWaiting, Is.False);
    }

    /// <summary>
    /// Tests that checking waiting for amount for a user waiting in the same chat returns true.
    /// </summary>
    [Test]
    public void IsWaitingForAmountUserWaitingInSameChatReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.userStateService.SetWaitingForAmount(userId, chatId);

        // Act
        var isWaiting = this.userStateService.IsWaitingForAmount(userId, chatId);

        // Assert
        Assert.That(isWaiting, Is.True);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Checked waiting for amount status") && v.ToString() !.Contains("True")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that removing waiting for amount for an existing user removes the state.
    /// </summary>
    [Test]
    public void RemoveWaitingForAmountExistingUserRemovesState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.userStateService.SetWaitingForAmount(userId, chatId);

        // Act
        this.userStateService.RemoveWaitingForAmount(userId);

        // Assert
        var isWaiting = this.userStateService.IsWaitingForAmount(userId, chatId);
        Assert.That(isWaiting, Is.False);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Removed user") && v.ToString() !.Contains("from waiting for amount state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that removing waiting for amount for a non-existent user logs a debug message.
    /// </summary>
    [Test]
    public void RemoveWaitingForAmountNonExistentUserLogsDebug()
    {
        // Arrange
        var userId = 999L; // Non-existent user

        // Act
        this.userStateService.RemoveWaitingForAmount(userId);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Attempted to remove non-existent waiting state for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that getting waiting users count returns zero when there are no users.
    /// </summary>
    [Test]
    public void GetWaitingUsersCountNoUsersReturnsZero()
    {
        // Arrange - no users in waiting state

        // Act
        var count = this.userStateService.GetWaitingUsersCount();

        // Assert
        Assert.That(count, Is.EqualTo(0));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Current users waiting for amount input: 0")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that getting waiting users count returns the correct count for multiple users.
    /// </summary>
    [Test]
    public void GetWaitingUsersCountMultipleUsersReturnsCorrectCount()
    {
        // Arrange
        this.userStateService.SetWaitingForAmount(123L, 456L);
        this.userStateService.SetWaitingForAmount(124L, 457L);
        this.userStateService.SetWaitingForAmount(125L, 458L);

        // Act
        var count = this.userStateService.GetWaitingUsersCount();

        // Assert
        Assert.That(count, Is.EqualTo(3));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Current users waiting for amount input: 3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that getting waiting users count is updated after removal.
    /// </summary>
    [Test]
    public void GetWaitingUsersCountAfterRemovalReturnsUpdatedCount()
    {
        // Arrange
        this.userStateService.SetWaitingForAmount(123L, 456L);
        this.userStateService.SetWaitingForAmount(124L, 457L);
        this.userStateService.SetWaitingForAmount(125L, 458L);

        // Act - remove one user
        this.userStateService.RemoveWaitingForAmount(124L);
        var count = this.userStateService.GetWaitingUsersCount();

        // Assert
        Assert.That(count, Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that clearing all waiting states with users clears all states.
    /// </summary>
    [Test]
    public void ClearAllWaitingStatesWithUsersClearsAllStates()
    {
        // Arrange
        this.userStateService.SetWaitingForAmount(123L, 456L);
        this.userStateService.SetWaitingForAmount(124L, 457L);
        this.userStateService.SetWaitingForAmount(125L, 458L);

        // Act
        this.userStateService.ClearAllWaitingStates();

        // Assert
        var count = this.userStateService.GetWaitingUsersCount();
        Assert.That(count, Is.EqualTo(0));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Cleared all waiting states, affected 3 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that clearing all waiting states with no users logs zero.
    /// </summary>
    [Test]
    public void ClearAllWaitingStatesNoUsersLogsZero()
    {
        // Arrange - no users

        // Act
        this.userStateService.ClearAllWaitingStates();

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Cleared all waiting states, affected 0 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that removing multiple waiting states for all existing users removes all.
    /// </summary>
    [Test]
    public void RemoveMultipleWaitingStatesAllUsersExistRemovesAll()
    {
        // Arrange
        this.userStateService.SetWaitingForAmount(123L, 456L);
        this.userStateService.SetWaitingForAmount(124L, 457L);
        this.userStateService.SetWaitingForAmount(125L, 458L);

        var userIdsToRemove = new long[] { 123L, 124L, 125L };

        // Act
        var removedCount = this.userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(3));

        var remainingCount = this.userStateService.GetWaitingUsersCount();
        Assert.That(remainingCount, Is.EqualTo(0));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Removed waiting states for 3 out of 3 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that removing multiple waiting states for some existing users removes only existing ones.
    /// </summary>
    [Test]
    public void RemoveMultipleWaitingStatesSomeUsersExistRemovesOnlyExisting()
    {
        // Arrange
        this.userStateService.SetWaitingForAmount(123L, 456L);
        this.userStateService.SetWaitingForAmount(124L, 457L);

        var userIdsToRemove = new long[] { 123L, 124L, 125L };

        // Act
        var removedCount = this.userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(2));

        var remainingCount = this.userStateService.GetWaitingUsersCount();
        Assert.That(remainingCount, Is.EqualTo(0));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Removed waiting states for 2 out of 3 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that removing multiple waiting states for non-existent users returns zero.
    /// </summary>
    [Test]
    public void RemoveMultipleWaitingStatesNoUsersExistReturnsZero()
    {
        // Arrange
        var userIdsToRemove = new long[] { 123L, 124L, 125L };

        // Act
        var removedCount = this.userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(0));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Removed waiting states for 0 out of 3 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that removing multiple waiting states with an empty list returns zero.
    /// </summary>
    [Test]
    public void RemoveMultipleWaitingStatesEmptyListReturnsZero()
    {
        // Arrange
        var userIdsToRemove = Array.Empty<long>();

        // Act
        var removedCount = this.userStateService.RemoveMultipleWaitingStates(userIdsToRemove);

        // Assert
        Assert.That(removedCount, Is.EqualTo(0));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Removed waiting states for 0 out of 0 users")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests a complex scenario with multiple operations works correctly.
    /// </summary>
    [Test]
    public void MultipleOperationsComplexScenarioWorksCorrectly()
    {
        // Arrange & Act - complex scenario
        this.userStateService.SetWaitingForAmount(123L, 456L);
        this.userStateService.SetWaitingForAmount(124L, 457L);

        var countAfterAdd = this.userStateService.GetWaitingUsersCount();
        Assert.That(countAfterAdd, Is.EqualTo(2));

        // Check states
        var user123Waiting = this.userStateService.IsWaitingForAmount(123L, 456L);
        var user124Waiting = this.userStateService.IsWaitingForAmount(124L, 457L);
        Assert.That(user123Waiting, Is.True);
        Assert.That(user124Waiting, Is.True);

        // Update state for one user
        this.userStateService.SetWaitingForAmount(123L, 789L);

        var user123OldChat = this.userStateService.IsWaitingForAmount(123L, 456L);
        var user123NewChat = this.userStateService.IsWaitingForAmount(123L, 789L);
        Assert.That(user123OldChat, Is.False);
        Assert.That(user123NewChat, Is.True);

        // Remove one user
        this.userStateService.RemoveWaitingForAmount(124L);

        var countAfterRemove = this.userStateService.GetWaitingUsersCount();
        Assert.That(countAfterRemove, Is.EqualTo(1));

        // Clear all
        this.userStateService.ClearAllWaitingStates();

        var finalCount = this.userStateService.GetWaitingUsersCount();
        Assert.That(finalCount, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that independent users do not interfere with each other's states.
    /// </summary>
    [Test]
    public void IndependentUsersDoNotInterfere()
    {
        // Arrange
        var user1 = 123L;
        var user2 = 124L;
        var chat1 = 456L;
        var chat2 = 457L;

        // Act
        this.userStateService.SetWaitingForAmount(user1, chat1);
        this.userStateService.SetWaitingForAmount(user2, chat2);

        // Assert - check that states are independent
        var user1InChat1 = this.userStateService.IsWaitingForAmount(user1, chat1);
        var user1InChat2 = this.userStateService.IsWaitingForAmount(user1, chat2);
        var user2InChat1 = this.userStateService.IsWaitingForAmount(user2, chat1);
        var user2InChat2 = this.userStateService.IsWaitingForAmount(user2, chat2);

        Assert.That(user1InChat1, Is.True);
        Assert.That(user1InChat2, Is.False);
        Assert.That(user2InChat1, Is.False);
        Assert.That(user2InChat2, Is.True);

        // Remove only user1
        this.userStateService.RemoveWaitingForAmount(user1);

        var user1AfterRemove = this.userStateService.IsWaitingForAmount(user1, chat1);
        var user2AfterRemove = this.userStateService.IsWaitingForAmount(user2, chat2);

        Assert.That(user1AfterRemove, Is.False);
        Assert.That(user2AfterRemove, Is.True);
    }
}