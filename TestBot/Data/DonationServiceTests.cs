using Data;
using Data.Models;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;

namespace Services.Tests;

[TestFixture]
public class DonationServiceTests
{
    private Mock<IDapperRepository> _repositoryMock;
    private Mock<ILogger<DonationService>> _loggerMock;
    private DonationService _donationService;

    [SetUp]
    public void Setup()
    {
        _repositoryMock = new Mock<IDapperRepository>();
        _loggerMock = new Mock<ILogger<DonationService>>();
        _donationService = new DonationService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task GetOrCreateUserAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var telegramId = 123L;
        var existingUser = new Users { Id = 1, TelegramId = telegramId, Username = "testuser", FirstName = "Test", LastName = "User" };

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _donationService.GetOrCreateUserAsync(telegramId, "testuser", "Test", "User");

        // Assert
        Assert.That(result, Is.EqualTo(existingUser));
        _repositoryMock.Verify(x => x.GetUserByTelegramIdAsync(telegramId), Times.Once);
        _repositoryMock.Verify(x => x.CreateUserAsync(It.IsAny<Users>()), Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Found existing user")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetOrCreateUserAsync_NewUser_CreatesUser()
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
            Admin = false
        };

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync((Users)null);

        _repositoryMock
            .Setup(x => x.CreateUserAsync(It.Is<Users>(u =>
                u.TelegramId == telegramId &&
                u.Username == "newuser" &&
                u.FirstName == "New" &&
                u.LastName == "User" &&
                u.Admin == false)))
            .ReturnsAsync(newUser);

        // Act
        var result = await _donationService.GetOrCreateUserAsync(telegramId, "newuser", "New", "User");

        // Assert
        Assert.That(result, Is.EqualTo(newUser));
        _repositoryMock.Verify(x => x.GetUserByTelegramIdAsync(telegramId), Times.Once);
        _repositoryMock.Verify(x => x.CreateUserAsync(It.IsAny<Users>()), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Creating new user") && v.ToString().Contains("newuser")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task GetOrCreateUserAsync_NullNames_HandlesCorrectly()
    {
        // Arrange
        var telegramId = 123L;
        var newUser = new Users { Id = 1, TelegramId = telegramId, Username = null, FirstName = null, LastName = null };

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ReturnsAsync((Users)null);

        _repositoryMock
            .Setup(x => x.CreateUserAsync(It.Is<Users>(u =>
                u.TelegramId == telegramId &&
                u.Username == null &&
                u.FirstName == null &&
                u.LastName == null)))
            .ReturnsAsync(newUser);

        // Act
        var result = await _donationService.GetOrCreateUserAsync(telegramId, null, null, null);

        // Assert
        Assert.That(result, Is.EqualTo(newUser));
    }

    [Test]
    public async Task GetOrCreateUserAsync_RepositoryThrows_LogsErrorAndThrows()
    {
        // Arrange
        var telegramId = 123L;
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.GetUserByTelegramIdAsync(telegramId))
            .ThrowsAsync(exception);

        // Act & Assert
        Assert.ThrowsAsync<Exception>(() =>
            _donationService.GetOrCreateUserAsync(telegramId, "testuser", "Test", "User"));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error getting or creating user")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task ProcessDonationAsync_SuccessfulDonation_ReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 10000, CurrentAmount = 0 };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount, Currency = currency, ProviderPaymentId = donationId };

        _repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null);

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.CreateDonationAsync(It.Is<Donation>(d =>
                d.UserTelegramId == userId &&
                d.GoalId == goal.Id &&
                d.Amount == amount &&
                d.Currency == currency &&
                d.ProviderPaymentId == donationId &&
                d.Status == "completed")))
            .ReturnsAsync(donation);

        _repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.True);

        _repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        _repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Once);
        _repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount), Times.Once);

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
    public async Task ProcessDonationAsync_DuplicateDonation_ReturnsTrue()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var goalId = 1;
        var currency = "RUB";
        var donationId = "donation_123";
        var existingDonation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goalId, Amount = amount, CreatedAt = DateTime.UtcNow };

        _repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync(existingDonation);

        // Act
        var result = await _donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.True);

        _repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        _repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Never);
        _repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Never);
        _repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("has already been processed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task ProcessDonationAsync_NoActiveGoal_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";

        _repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null);

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null);

        // Act
        var result = await _donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.False);

        _repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        _repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Never);
        _repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No active goal found for donation")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task ProcessDonationAsync_CreateDonationThrows_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null);

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.CreateDonationAsync(It.IsAny<Donation>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.False);

        _repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        _repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Once);
        _repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing donation")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task ProcessDonationAsync_UpdateGoalThrows_ReturnsFalse()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount };
        var exception = new Exception("Database error");

        _repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null);

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.CreateDonationAsync(It.IsAny<Donation>()))
            .ReturnsAsync(donation);

        _repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .ThrowsAsync(exception);

        // Act
        var result = await _donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.False);

        _repositoryMock.Verify(x => x.GetDonationAsync(donationId), Times.Once);
        _repositoryMock.Verify(x => x.GetActiveGoalAsync(), Times.Once);
        _repositoryMock.Verify(x => x.CreateDonationAsync(It.IsAny<Donation>()), Times.Once);
        _repositoryMock.Verify(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount), Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing donation")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task ProcessDonationAsync_ValidatesDonationStatus()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount };

        _repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null);

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.CreateDonationAsync(It.Is<Donation>(d => d.Status == "completed")))
            .ReturnsAsync(donation);

        _repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert
        Assert.That(result, Is.True);

        // Проверяем, что статус доната установлен в "completed"
        _repositoryMock.Verify(x => x.CreateDonationAsync(It.Is<Donation>(d => d.Status == "completed")), Times.Once);
    }

    [Test]
    public async Task ProcessDonationAsync_LogsAppropriateMessages()
    {
        // Arrange
        var userId = 123L;
        var amount = 500m;
        var currency = "RUB";
        var donationId = "donation_123";
        var goal = new DonationGoal { Id = 1, Title = "Test Goal" };
        var donation = new Donation { Id = 1, UserTelegramId = userId, GoalId = goal.Id, Amount = amount };

        _repositoryMock
            .Setup(x => x.GetDonationAsync(donationId))
            .ReturnsAsync((Donation)null);

        _repositoryMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(goal);

        _repositoryMock
            .Setup(x => x.CreateDonationAsync(It.IsAny<Donation>()))
            .ReturnsAsync(donation);

        _repositoryMock
            .Setup(x => x.UpdateGoalCurrentAmountAsync(goal.Id, amount))
            .Returns(Task.CompletedTask);

        // Act
        await _donationService.ProcessDonationAsync(userId, amount, currency, donationId);

        // Assert - проверяем последовательность логирования
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing donation")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Found active goal")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Created donation record")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }
}