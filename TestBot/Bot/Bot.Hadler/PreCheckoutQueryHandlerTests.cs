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

/// <summary>
/// Unit tests for the <see cref="PreCheckoutQueryHandler"/> class.
/// </summary>
[TestFixture]
public class PreCheckoutQueryHandlerTests
{
    private Mock<ILogger<PreCheckoutQueryHandler>> loggerMock;
    private Mock<IGoalService> goalServiceMock;
    private Mock<ITelegramBotClient> botClientMock;
    private PreCheckoutQueryHandler handler;

    /// <summary>
    /// Sets up the test environment before each test execution.
    /// </summary>
    [SetUp]
    public void Setup()
    {
        this.loggerMock = new Mock<ILogger<PreCheckoutQueryHandler>>();
        this.goalServiceMock = new Mock<IGoalService>();
        this.botClientMock = new Mock<ITelegramBotClient>();

        this.handler = new PreCheckoutQueryHandler(
            this.loggerMock.Object,
            this.goalServiceMock.Object);
    }

    /// <summary>
    /// Tests that the handler correctly identifies updates with pre-checkout queries.
    /// </summary>
    [Test]
    public void CanHandleWithPreCheckoutQueryReturnsTrue()
    {
        // Arrange
        var update = new Update { PreCheckoutQuery = new PreCheckoutQuery() };

        // Act
        var result = this.handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Tests that the handler correctly identifies updates without pre-checkout queries.
    /// </summary>
    [Test]
    public void CanHandleWithoutPreCheckoutQueryReturnsFalse()
    {
        // Arrange
        var update = new Update { PreCheckoutQuery = null };

        // Act
        var result = this.handler.CanHandle(update);

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Tests that a pre-checkout query with null user triggers a warning log and returns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncPreCheckoutQueryWithNullUserLogsWarningAndReturns()
    {
        // Arrange
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = null, // Null user
            InvoicePayload = "test_payload",
            TotalAmount = 10000, // 100.00 RUB
            Currency = "RUB",
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = CancellationToken.None;

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received pre-checkout query with null user information")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify that business logic is not called
        this.goalServiceMock.Verify(
            x => x.GetActiveGoalAsync(),
            Times.Never);
    }

    /// <summary>
    /// Tests that a pre-checkout query with an active goal is approved.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncWithActiveGoalApprovesPreCheckoutQuery()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "test_payload",
            TotalAmount = 10000, // 100.00 RUB
            Currency = "RUB",
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = CancellationToken.None;

        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 100000 };
        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Approved pre-checkout query")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that a pre-checkout query without an active goal is rejected.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncWithoutActiveGoalRejectsPreCheckoutQuery()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "test_payload",
            TotalAmount = 10000,
            Currency = "RUB",
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(null as DonationGoal); // No active goal

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Rejected pre-checkout query") && v.ToString() !.Contains("no active goal found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that exceptions in goal service are logged and handled safely.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncGoalServiceThrowsExceptionLogsErrorAndCallsSafeAnswer()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "test_payload",
            TotalAmount = 10000,
            Currency = "RUB",
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error handling pre-checkout query")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that approval logs contain correct details.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncWithActiveGoalLogsCorrectDetails()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "donation_500",
            TotalAmount = 50000, // 500.00 RUB
            Currency = "RUB",
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = CancellationToken.None;

        var activeGoal = new DonationGoal { Id = 1, Title = "Test Goal", TargetAmount = 100000 };
        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(activeGoal);

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert - verify that logs contain correct details
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString() !.Contains("Approved pre-checkout query") &&
                    v.ToString() !.Contains("123") && // user ID
                    v.ToString() !.Contains("donation_500") && // payload
                    v.ToString() !.Contains("500") && // amount
                    v.ToString() !.Contains("RUB")), // currency
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that rejection logs contain correct details.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task HandleAsyncWithoutActiveGoalLogsRejectionDetails()
    {
        // Arrange
        var user = new User { Id = 123 };
        var preCheckoutQuery = new PreCheckoutQuery
        {
            Id = "test_query_id",
            From = user,
            InvoicePayload = "donation_500",
            TotalAmount = 50000,
            Currency = "RUB",
        };
        var update = new Update { PreCheckoutQuery = preCheckoutQuery };
        var cancellationToken = CancellationToken.None;

        this.goalServiceMock
            .Setup(x => x.GetActiveGoalAsync())
            .ReturnsAsync(null as DonationGoal);

        // Act
        await this.handler.HandleAsync(this.botClientMock.Object, update, cancellationToken);

        // Assert - verify that logs contain correct rejection details
        this.loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString() !.Contains("Rejected pre-checkout query") &&
                    v.ToString() !.Contains("123") && // user ID
                    v.ToString() !.Contains("no active goal found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}