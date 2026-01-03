// <copyright file="DonationServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;
using Data;
using Data.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;

namespace Services.Tests;

/// <summary>
/// Contains unit tests for the <see cref="DonationService"/> class.
/// Tests various scenarios including user management, donation processing,
/// exception handling, and logging behavior.
/// </summary>
[TestFixture]
public class DonationServiceTests
{
    private Mock<IDapperRepository> repositoryMock;
    private Mock<ILogger<DonationService>> loggerMock;
    private DonationService donationService;

    /// <summary>
    /// Initializes test environment before each test execution.
    /// Creates mock instances for repository and logger, and instantiates the donation service.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        repositoryMock = new Mock<IDapperRepository>();
        loggerMock = new Mock<ILogger<DonationService>>();
        donationService = new DonationService(repositoryMock.Object, loggerMock.Object);
    }

    /// <summary>
    /// Tests that when a user already exists in the database,
    /// the method returns the existing user without creating a new one.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetOrCreateUserAsyncExistingUserReturnsUser()
    {
        // Arrange
        var telegramId = 123L;
        var existingUser = new Users { Id = 1, TelegramId = telegramId, Username = "testuser", FirstName = "Test", LastName = "User" };

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync(existingUser);

        // Act
        var result = await donationService.GetOrCreateUserAsync(telegramId, "testuser", "Test", "User");

        // Assert
        Assert.That(result, Is.EqualTo(existingUser));
        repositoryMock.Verify(x => x.GetUserByTelegramIdAsync(telegramId), Times.Once);
        repositoryMock.Verify(x => x.CreateUserAsync(It.IsAny<Users>()), Times.Never);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Found existing user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when a user does not exist in the database,
    /// the method creates a new user with the provided information.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetOrCreateUserAsyncNewUserCreatesUser()
    {
        // Arrange
        var telegramId = 123L;
        var newUser = new Users
        {
            Id = 1,
            TelegramId = telegramId,
            Username = "newuser",
            FirstName = "New",
            LastName = "User",
            Admin = false,
        };

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync((Users)null!);

        repositoryMock
            .Setup(x => x.CreateUserAsync(It.Is<Users>(u =>
                u.TelegramId == telegramId &&
                u.Username == "newuser" &&
                u.FirstName == "New" &&
                u.LastName == "User" &&
                u.Admin == false)))
            .ReturnsAsync(newUser);

        // Act
        var result = await donationService.GetOrCreateUserAsync(telegramId, "newuser", "New", "User");

        // Assert
        Assert.That(result, Is.EqualTo(newUser));
        repositoryMock.Verify(x => x.GetUserByTelegramIdAsync(telegramId), Times.Once);
        repositoryMock.Verify(x => x.CreateUserAsync(It.IsAny<Users>()), Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Creating new user") && v.ToString() !.Contains("newuser")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the method correctly handles null values for user properties
    /// when creating a new user.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetOrCreateUserAsyncNullNamesHandlesCorrectly()
    {
        // Arrange
        var telegramId = 123L;
        var newUser = new Users { Id = 1, TelegramId = telegramId, Username = null, FirstName = null, LastName = null };

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync((Users)null!);

        repositoryMock
            .Setup(x => x.CreateUserAsync(It.Is<Users>(u =>
                u.TelegramId == telegramId &&
                u.Username == null &&
                u.FirstName == null &&
                u.LastName == null)))
            .ReturnsAsync(newUser);

        // Act
        var result = await donationService.GetOrCreateUserAsync(telegramId, null, null, null);

        // Assert
        Assert.That(result, Is.EqualTo(newUser));
    }

    /// <summary>
    /// Tests that when the repository throws an exception,
    /// the method logs the error and rethrows the exception.
    /// </summary>
    [Test]
    public void GetOrCreateUserAsyncRepositoryThrowsLogsErrorAndThrows()
    {
        // Arrange
        var telegramId = 123L;
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ThrowsAsync(exception);

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() =>
            donationService.GetOrCreateUserAsync(telegramId, "testuser", "Test", "User"));

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error getting or creating user")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a successful donation is processed correctly,
    /// creating a donation record and updating the goal amount.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProcessDonationAsyncSuccessfulDonationReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 10000, CurrentAmount = 0 };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount, Currency = currency, ProviderPaymentId = donationId };

        repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null!);

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.CreateDonationAsync(It.Is<Donation>(d =>
                d.UserTelegramId == userId &&
                d.GoalId == goal.Id &&
                d.Amount == amount &&
                d.Currency == currency &&
                d.ProviderPaymentId == donationId &&
                d.Status == "completed")))
            .ReturnsAsync(donation);

        repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .Returns(Task.CompletedTask);

        // Act
        var result = await donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.True);

        repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Once);
        repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount), Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Successfully processed donation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when a duplicate donation is detected (same donation ID),
    /// the method returns true without creating a new donation record.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProcessDonationAsyncDuplicateDonationReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var goalId = 1;
        var currency = "RUB";
        var donationId = "donation_123";
        var existingDonation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goalId, Amount = amount, CreatedAt = DateTime.UtcNow };

        repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync(existingDonation);

        // Act
        var result = await donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.True);

        repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Never);
        repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Never);
        repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("has already been processed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when there is no active donation goal,
    /// the method returns false and logs an error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProcessDonationAsyncNoActiveGoalReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";

        repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null!);

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null!);

        // Act
        var result = await donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.False);

        repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Never);
        repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No active goal found for donation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when creating a donation record throws an exception,
    /// the method returns false and logs the error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProcessDonationAsyncCreateDonationThrowsReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null!);

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.CreateDonationAsync(It.IsAny<Donation>()))
            .ThrowsAsync(exception);

        // Act
        var result = await donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.False);

        repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Once);
        repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error processing donation")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that when updating the goal amount throws an exception,
    /// the method returns false and logs the error.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProcessDonationAsyncUpdateGoalThrowsReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount };
        var exception = new Exception("Database error");

        repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null!);

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.CreateDonationAsync(It.IsAny<Donation>()))
            .ReturnsAsync(donation);

        repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .ThrowsAsync(exception);

        // Act
        var result = await donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.False);

        repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Once);
        repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount), Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error processing donation")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that the donation record is created with the correct status ("completed").
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProcessDonationAsyncValidatesDonationStatus()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount };

        repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null!);

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.CreateDonationAsync(It.Is<Donation>(d => d.Status == "completed")))
            .ReturnsAsync(donation);

        repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .Returns(Task.CompletedTask);

        // Act
        var result = await donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.True);

        // Проверяем, что статус доната установлен в "completed"
        repositoryMock.Verify(x => x.CreateDonationAsync(It.Is<Donation>(d => d.Status == "completed")), Times.Once);
    }

    /// <summary>
    /// Tests that appropriate log messages are recorded during donation processing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ProcessDonationAsyncLogsAppropriateMessages()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount };

        repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null!);

        repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        repositoryMock
            .Setup(x => x.CreateDonationAsync(It.IsAny<Donation>()))
            .ReturnsAsync(donation);

        repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .Returns(Task.CompletedTask);

        // Act
        await donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert - проверяем последовательность логирования
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Processing donation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Found active goal")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Created donation record")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}