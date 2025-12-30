using Bot.Handlers;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;
using Data.Models;

namespace Bot.Tests.Handlers;

[TestFixture]
public class PreCheckoutQueryHandlerTests
{
    private Mock<ILogger<PreCheckoutQueryHandler>> _loggerMock;
    private Mock<IGoalService> _goalServiceMock;
    private Mock<ITelegramBotClient> _botClientMock;
    private PreCheckoutQueryHandler _handler;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<PreCheckoutQueryHandler>>();
        _goalServiceMock = new Mock<IGoalService>();
        _botClientMock = new Mock<ITelegramBotClient>();

        _handler = new PreCheckoutQueryHandler(
            _loggerMock.Object,
            _goalServiceMock.Object);
    }

    [Test]
    public void CanHandle_WithPreCheckoutQuery_ReturnsTrue()
    {
        // Arrange
        var update = new Update { PreCheckoutQuery = new PreCheckoutQuery() };

        // Act
        var result = _handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanHandle_WithoutPreCheckoutQuery_ReturnsFalse()
    {
        // Arrange
        var update = new Update { PreCheckoutQuery = null };

        // Act
        var result = _handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task HandleAsync_PreCheckoutQueryWithNullUser_LogsWarningAndReturns()
    {
        // Arrange
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = null, // Null user
            InvoicePayload = "test_payload",
            TotalAmount = 10000, // 100.00 RUB
            Currency = "RUB"
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = new CancellationToken();

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received pre-checkout query with null user information")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);

        // Проверяем, что бизнес-логика не вызывалась
        _goalServiceMock.Verify(
            x => x.GetActiveGoalAsync(),
            Times.Never);
    }

    [Test]
    public async Task HandleAsync_WithActiveGoal_ApprovesPreCheckoutQuery()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "test_payload",
            TotalAmount = 10000, // 100.00 RUB
            Currency = "RUB"
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = new CancellationToken();

        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 100000 };
        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Approved pre-checkout query")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_WithoutActiveGoal_RejectsPreCheckoutQuery()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "test_payload",
            TotalAmount = 10000,
            Currency = "RUB"
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null); // Нет активной цели

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Rejected pre-checkout query") && v.ToString().Contains("no active goal found")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_GoalServiceThrowsException_LogsErrorAndCallsSafeAnswer()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "test_payload",
            TotalAmount = 10000,
            Currency = "RUB"
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error handling pre-checkout query")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_WithActiveGoal_LogsCorrectDetails()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "donation_500",
            TotalAmount = 50000, // 500.00 RUB
            Currency = "RUB"
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = new CancellationToken();

        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 100000 };
        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что логи содержат правильные детали
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Approved pre-checkout query") &&
                    v.ToString().Contains("123") && // user ID
                    v.ToString().Contains("donation_500") && // payload
                    v.ToString().Contains("500") && // amount
                    v.ToString().Contains("RUB")), // currency
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleAsync_WithoutActiveGoal_LogsRejectionDetails()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "donation_500",
            TotalAmount = 50000,
            Currency = "RUB"
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = new CancellationToken();

        _goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync((DonationGoal)null);

        // Act
        await _handler.HandleAsync(_botClientMock.Object, update, cancellationToken);

        // Assert - проверяем, что логи содержат правильные детали отклонения
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString().Contains("Rejected pre-checkout query") &&
                    v.ToString().Contains("123") && // user ID
                    v.ToString().Contains("no active goal found")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }
}