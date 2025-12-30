using Bot;
using Bot.Handlers;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;

namespace Bot.Tests;

[TestFixture]
public class UpdateHandlerTests
{
    private Mock<ILogger<UpdateHandler>> _loggerMock;
    private Mock<ITelegramBotClient> _botClientMock;
    private List<Mock<IUpdateHandlerCommand>> _handlerMocks;
    private UpdateHandler _updateHandler;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<UpdateHandler>>();
        _botClientMock = new Mock<ITelegramBotClient>();

        // Создаем моки обработчиков
        _handlerMocks = new List<Mock<IUpdateHandlerCommand>>();

        var handlers = new List<IUpdateHandlerCommand>();
        _updateHandler = new UpdateHandler(_loggerMock.Object, handlers);
    }

    private void AddHandler(Mock<IUpdateHandlerCommand> handlerMock)
    {
        _handlerMocks.Add(handlerMock);
        var handlers = _handlerMocks.Select(m => m.Object).ToList();
        _updateHandler = new UpdateHandler(_loggerMock.Object, handlers);
    }

    [Test]
    public async Task HandleUpdateAsync_NoHandlers_LogsWarning()
    {
        // Arrange
        var update = new Update { Id = 123 };
        var cancellationToken = new CancellationToken();

        // Act
        await _updateHandler.HandleUpdateAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No handler found for update")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleUpdateAsync_FirstMatchingHandlerUsed_DoesNotCheckOtherHandlers()
    {
        // Arrange
        var update = new Update { Id = 123, CallbackQuery = new CallbackQuery() };
        var cancellationToken = new CancellationToken();

        var firstHandlerMock = new Mock<IUpdateHandlerCommand>();
        firstHandlerMock.Setup(h => h.CanHandle(update)).Returns(true);
        firstHandlerMock.Setup(h => h.HandleAsync(_botClientMock.Object, update, cancellationToken))
                       .Returns(Task.CompletedTask);

        var secondHandlerMock = new Mock<IUpdateHandlerCommand>();
        secondHandlerMock.Setup(h => h.CanHandle(update)).Returns(true); // Этот тоже подходит, но не должен быть вызван

        AddHandler(firstHandlerMock);
        AddHandler(secondHandlerMock);

        // Act
        await _updateHandler.HandleUpdateAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        firstHandlerMock.Verify(h => h.CanHandle(update), Times.Once);
        firstHandlerMock.Verify(h => h.HandleAsync(_botClientMock.Object, update, cancellationToken), Times.Once);
        secondHandlerMock.Verify(h => h.CanHandle(update), Times.Never); // Не должен проверяться
    }

    [Test]
    public async Task HandleUpdateAsync_HandlerThrowsException_LogsError()
    {
        // Arrange
        var update = new Update { Id = 123, Message = new Message() };
        var cancellationToken = new CancellationToken();
        var exception = new Exception("Handler failed");

        var handlerMock = new Mock<IUpdateHandlerCommand>();
        handlerMock.Setup(h => h.CanHandle(update)).Returns(true);
        handlerMock.Setup(h => h.HandleAsync(_botClientMock.Object, update, cancellationToken))
                  .ThrowsAsync(exception);

        AddHandler(handlerMock);

        // Act
        await _updateHandler.HandleUpdateAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error processing update")),
                exception,
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleUpdateAsync_LogsDebugOnReceipt()
    {
        // Arrange
        var update = new Update { Id = 123 };
        var cancellationToken = new CancellationToken();

        // Act
        await _updateHandler.HandleUpdateAsync(_botClientMock.Object, update, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Received update") && v.ToString().Contains("123")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public void Constructor_WithHandlers_LogsHandlerCount()
    {
        // Arrange
        var handlers = new List<IUpdateHandlerCommand>
        {
            new Mock<IUpdateHandlerCommand>().Object,
            new Mock<IUpdateHandlerCommand>().Object,
            new Mock<IUpdateHandlerCommand>().Object
        };

        // Act
        var handler = new UpdateHandler(_loggerMock.Object, handlers);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("UpdateHandler initialized with 3 handlers")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Test]
    public async Task HandleUpdateAsync_MultipleHandlerTypes_RoutesCorrectly()
    {
        // Arrange
        var messageUpdate = new Update { Id = 1, Message = new Message() };
        var callbackUpdate = new Update { Id = 2, CallbackQuery = new CallbackQuery() };
        var preCheckoutUpdate = new Update { Id = 3, PreCheckoutQuery = new PreCheckoutQuery() };
        var cancellationToken = new CancellationToken();

        var messageHandlerMock = new Mock<IUpdateHandlerCommand>();
        messageHandlerMock.Setup(h => h.CanHandle(It.Is<Update>(u => u.Message != null))).Returns(true);

        var callbackHandlerMock = new Mock<IUpdateHandlerCommand>();
        callbackHandlerMock.Setup(h => h.CanHandle(It.Is<Update>(u => u.CallbackQuery != null))).Returns(true);

        var preCheckoutHandlerMock = new Mock<IUpdateHandlerCommand>();
        preCheckoutHandlerMock.Setup(h => h.CanHandle(It.Is<Update>(u => u.PreCheckoutQuery != null))).Returns(true);

        AddHandler(messageHandlerMock);
        AddHandler(callbackHandlerMock);
        AddHandler(preCheckoutHandlerMock);

        // Act & Assert - Message update
        await _updateHandler.HandleUpdateAsync(_botClientMock.Object, messageUpdate, cancellationToken);
        messageHandlerMock.Verify(h => h.HandleAsync(_botClientMock.Object, messageUpdate, cancellationToken), Times.Once);
        callbackHandlerMock.Verify(h => h.HandleAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Update>(), It.IsAny<CancellationToken>()), Times.Never);
        preCheckoutHandlerMock.Verify(h => h.HandleAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Update>(), It.IsAny<CancellationToken>()), Times.Never);

        // Act & Assert - Callback update
        await _updateHandler.HandleUpdateAsync(_botClientMock.Object, callbackUpdate, cancellationToken);
        callbackHandlerMock.Verify(h => h.HandleAsync(_botClientMock.Object, callbackUpdate, cancellationToken), Times.Once);

        // Act & Assert - PreCheckout update
        await _updateHandler.HandleUpdateAsync(_botClientMock.Object, preCheckoutUpdate, cancellationToken);
        preCheckoutHandlerMock.Verify(h => h.HandleAsync(_botClientMock.Object, preCheckoutUpdate, cancellationToken), Times.Once);
    }
}