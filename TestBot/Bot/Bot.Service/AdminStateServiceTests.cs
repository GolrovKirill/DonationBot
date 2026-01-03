// <copyright file="AdminStateServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using static Bot.Services.AdminStateService;

namespace Bot.Tests.Services;

/// <summary>
/// Unit tests for the <see cref="AdminStateService"/> class.
/// </summary>
[TestFixture]
public class AdminStateServiceTests
{
    private Mock<ILogger<AdminStateService>> loggerMock;
    private AdminStateService adminStateService;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this.loggerMock = new Mock<ILogger<AdminStateService>>();
        this.adminStateService = new AdminStateService(this.loggerMock.Object);
    }

    /// <summary>
    /// Tests that starting goal creation for a new user sets the initial state correctly.
    /// </summary>
    [Test]
    public void StartGoalCreationNewUserSetsInitialState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;

        // Act
        this.adminStateService.StartGoalCreation(userId, chatId);

        // Assert
        var state = this.adminStateService.GetState(userId);
        Assert.That(state, Is.Not.Null);
        Assert.That(state.ChatId, Is.EqualTo(chatId));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForTitle));
        Assert.That(state.Title, Is.Null);
        Assert.That(state.Description, Is.Null);
        Assert.That(state.TargetAmount, Is.Null);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Started goal creation for admin user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that starting goal creation for an existing user overwrites the previous state.
    /// </summary>
    [Test]
    public void StartGoalCreationExistingUserOverwritesState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.adminStateService.StartGoalCreation(userId, chatId);

        // Act - start goal creation again
        this.adminStateService.StartGoalCreation(userId, 789L); // New chatId

        // Assert
        var state = this.adminStateService.GetState(userId);
        Assert.That(state.ChatId, Is.EqualTo(789L));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForTitle));
    }

    /// <summary>
    /// Tests that getting state for a non-existent user returns null.
    /// </summary>
    [Test]
    public void GetStateNonExistentUserReturnsNull()
    {
        // Arrange
        var userId = 999L; // Non-existent user

        // Act
        var state = this.adminStateService.GetState(userId);

        // Assert
        Assert.That(state, Is.Null);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No state found for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that getting state for an existing user returns the correct state.
    /// </summary>
    [Test]
    public void GetStateExistingUserReturnsState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.adminStateService.StartGoalCreation(userId, chatId);

        // Act
        var state = this.adminStateService.GetState(userId);

        // Assert
        Assert.That(state, Is.Not.Null);
        Assert.That(state.ChatId, Is.EqualTo(chatId));
    }

    /// <summary>
    /// Tests that setting title for an existing user updates the title and advances the step.
    /// </summary>
    [Test]
    public void SetTitleExistingUserSetsTitleAndAdvancesStep()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        var title = "Новая цель";
        this.adminStateService.StartGoalCreation(userId, chatId);

        // Act
        this.adminStateService.SetTitle(userId, title);

        // Assert
        var state = this.adminStateService.GetState(userId);
        Assert.That(state.Title, Is.EqualTo(title));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForDescription));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Set title for user") && v.ToString() !.Contains(title)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that setting title for a non-existent user logs a warning.
    /// </summary>
    [Test]
    public void SetTitleNonExistentUserLogsWarning()
    {
        // Arrange
        var userId = 999L; // Non-existent user
        var title = "Новая цель";

        // Act
        this.adminStateService.SetTitle(userId, title);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Attempted to set title for non-existent user state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that setting description for an existing user updates the description and advances the step.
    /// </summary>
    [Test]
    public void SetDescriptionExistingUserSetsDescriptionAndAdvancesStep()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        var description = "Описание цели";
        this.adminStateService.StartGoalCreation(userId, chatId);
        this.adminStateService.SetTitle(userId, "Название");

        // Act
        this.adminStateService.SetDescription(userId, description);

        // Assert
        var state = this.adminStateService.GetState(userId);
        Assert.That(state.Description, Is.EqualTo(description));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.WaitingForAmount));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Set description for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that setting description for a non-existent user logs a warning.
    /// </summary>
    [Test]
    public void SetDescriptionNonExistentUserLogsWarning()
    {
        // Arrange
        var userId = 999L; // Non-existent user
        var description = "Описание цели";

        // Act
        this.adminStateService.SetDescription(userId, description);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Attempted to set description for non-existent user state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that setting amount for an existing user updates the amount and completes the process.
    /// </summary>
    [Test]
    public void SetAmountExistingUserSetsAmountAndCompletesProcess()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        var amount = 5000m;
        this.adminStateService.StartGoalCreation(userId, chatId);
        this.adminStateService.SetTitle(userId, "Название");
        this.adminStateService.SetDescription(userId, "Описание");

        // Act
        this.adminStateService.SetAmount(userId, amount);

        // Assert
        var state = this.adminStateService.GetState(userId);
        Assert.That(state.TargetAmount, Is.EqualTo(amount));
        Assert.That(state.CurrentStep, Is.EqualTo(AdminGoalStep.None));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Set amount for user") && v.ToString() !.Contains(amount.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that setting amount for a non-existent user logs a warning.
    /// </summary>
    [Test]
    public void SetAmountNonExistentUserLogsWarning()
    {
        // Arrange
        var userId = 999L; // Non-existent user
        var amount = 5000m;

        // Act
        this.adminStateService.SetAmount(userId, amount);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Attempted to set amount for non-existent user state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that canceling goal creation for an existing user removes the state.
    /// </summary>
    [Test]
    public void CancelGoalCreationExistingUserRemovesState()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.adminStateService.StartGoalCreation(userId, chatId);

        // Act
        this.adminStateService.CancelGoalCreation(userId);

        // Assert
        var state = this.adminStateService.GetState(userId);
        Assert.That(state, Is.Null);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Canceled goal creation for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that canceling goal creation for a non-existent user logs a debug message.
    /// </summary>
    [Test]
    public void CancelGoalCreationNonExistentUserLogsDebug()
    {
        // Arrange
        var userId = 999L; // Non-existent user

        // Act
        this.adminStateService.CancelGoalCreation(userId);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Attempted to cancel non-existent goal creation for user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a user in the goal creation process returns true.
    /// </summary>
    [Test]
    public void IsUserCreatingGoalUserInProcessReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.adminStateService.StartGoalCreation(userId, chatId);

        // Act
        var isCreating = this.adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.True);

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("User") && v.ToString() !.Contains("goal creation status: True")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a user not in the goal creation process returns false.
    /// </summary>
    [Test]
    public void IsUserCreatingGoalUserNotInProcessReturnsFalse()
    {
        // Arrange
        var userId = 123L; // User has not started goal creation

        // Act
        var isCreating = this.adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.False);
    }

    /// <summary>
    /// Tests that a user who completed the goal creation process returns false.
    /// </summary>
    [Test]
    public void IsUserCreatingGoalUserCompletedProcessReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.adminStateService.StartGoalCreation(userId, chatId);
        this.adminStateService.SetTitle(userId, "Название");
        this.adminStateService.SetDescription(userId, "Описание");
        this.adminStateService.SetAmount(userId, 5000m); // Complete the process

        // Act
        var isCreating = this.adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.False);
    }

    /// <summary>
    /// Tests that a user who cancelled the goal creation process returns false.
    /// </summary>
    [Test]
    public void IsUserCreatingGoalUserCancelledProcessReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var chatId = 456L;
        this.adminStateService.StartGoalCreation(userId, chatId);
        this.adminStateService.CancelGoalCreation(userId); // Cancel the process

        // Act
        var isCreating = this.adminStateService.IsUserCreatingGoal(userId);

        // Assert
        Assert.That(isCreating, Is.False);
    }

    /// <summary>
    /// Tests that the active state count returns zero when there are no users.
    /// </summary>
    [Test]
    public void GetActiveStateCountNoUsersReturnsZero()
    {
        // Arrange - no active users

        // Act
        var count = this.adminStateService.GetActiveStateCount();

        // Assert
        Assert.That(count, Is.EqualTo(0));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Current active admin states: 0")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the active state count returns the correct count for multiple users.
    /// </summary>
    [Test]
    public void GetActiveStateCountMultipleUsersReturnsCorrectCount()
    {
        // Arrange
        this.adminStateService.StartGoalCreation(123L, 456L);
        this.adminStateService.StartGoalCreation(124L, 457L);
        this.adminStateService.StartGoalCreation(125L, 458L);

        // Act
        var count = this.adminStateService.GetActiveStateCount();

        // Assert
        Assert.That(count, Is.EqualTo(3));

        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Current active admin states: 3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the active state count is updated after cancellation.
    /// </summary>
    [Test]
    public void GetActiveStateCountAfterCancellationReturnsUpdatedCount()
    {
        // Arrange
        this.adminStateService.StartGoalCreation(123L, 456L);
        this.adminStateService.StartGoalCreation(124L, 457L);
        this.adminStateService.StartGoalCreation(125L, 458L);

        // Act - cancel one user
        this.adminStateService.CancelGoalCreation(124L);
        var count = this.adminStateService.GetActiveStateCount();

        // Assert
        Assert.That(count, Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that multiple users have independent states.
    /// </summary>
    [Test]
    public void MultipleUsersIndependentStates()
    {
        // Arrange
        var user1 = 123L;
        var user2 = 124L;
        var chat1 = 456L;
        var chat2 = 457L;

        // Act
        this.adminStateService.StartGoalCreation(user1, chat1);
        this.adminStateService.StartGoalCreation(user2, chat2);

        this.adminStateService.SetTitle(user1, "Цель пользователя 1");
        this.adminStateService.SetTitle(user2, "Цель пользователя 2");

        // Assert
        var state1 = this.adminStateService.GetState(user1);
        var state2 = this.adminStateService.GetState(user2);

        Assert.That(state1.Title, Is.EqualTo("Цель пользователя 1"));
        Assert.That(state2.Title, Is.EqualTo("Цель пользователя 2"));
        Assert.That(state1.ChatId, Is.EqualTo(chat1));
        Assert.That(state2.ChatId, Is.EqualTo(chat2));
    }
}