// <copyright file="UpdateHandlerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bot.Handlers;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;

namespace Bot.Tests
{
    /// <summary>
    /// Contains unit tests for the <see cref="UpdateHandler"/> class.
    /// Tests various scenarios including update routing, exception handling,
    /// logging behavior, and handler selection logic.
    /// </summary>
    [TestFixture]
    public class UpdateHandlerTests
    {
        private Mock<ILogger<UpdateHandler>> loggerMock;
        private Mock<ITelegramBotClient> botClientMock;
        private List<Mock<IUpdateHandlerCommand>> handlerMocks;
        private UpdateHandler updateHandler;

        /// <summary>
        /// Initializes test setup before each test execution.
        /// Creates mock instances for logger, bot client, and prepares empty handler list.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            loggerMock = new Mock<ILogger<UpdateHandler>>();
            botClientMock = new Mock<ITelegramBotClient>();

            // Create mock handlers
            handlerMocks = new List<Mock<IUpdateHandlerCommand>>();

            var handlers = new List<IUpdateHandlerCommand>();
            updateHandler = new UpdateHandler(loggerMock.Object, handlers);
        }

        /// <summary>
        /// Tests that a warning log is recorded when no handlers are available to process an update.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Test]
        public async Task HandleUpdateAsyncNoHandlersLogsWarning()
        {
            // Arrange
            var update = new Update { Id = 123 };
            var cancellationToken = CancellationToken.None;

            // Act
            await updateHandler.HandleUpdateAsync(botClientMock.Object, update, cancellationToken);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("No handler found for update")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that only the first matching handler is executed when multiple handlers could process an update.
        /// Verifies that subsequent matching handlers are not checked or executed.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Test]
        public async Task HandleUpdateAsyncFirstMatchingHandlerUsedDoesNotCheckOtherHandlers()
        {
            // Arrange
            var update = new Update { Id = 123, CallbackQuery = new CallbackQuery() };
            var cancellationToken = CancellationToken.None;

            var firstHandlerMock = new Mock<IUpdateHandlerCommand>();
            firstHandlerMock.Setup(h => h.CanHandle(update)).Returns(true);
            firstHandlerMock.Setup(h => h.HandleAsync(botClientMock.Object, update, cancellationToken))
                           .Returns(Task.CompletedTask);

            var secondHandlerMock = new Mock<IUpdateHandlerCommand>();

            // This one also matches but should not be called
            secondHandlerMock.Setup(h => h.CanHandle(update)).Returns(true);

            AddHandler(firstHandlerMock);
            AddHandler(secondHandlerMock);

            // Act
            await updateHandler.HandleUpdateAsync(botClientMock.Object, update, cancellationToken);

            // Assert
            firstHandlerMock.Verify(h => h.CanHandle(update), Times.Once);
            firstHandlerMock.Verify(h => h.HandleAsync(botClientMock.Object, update, cancellationToken), Times.Once);

            // Should not be checked
            secondHandlerMock.Verify(h => h.CanHandle(update), Times.Never);
        }

        /// <summary>
        /// Tests that an error log is recorded when a handler throws an exception during update processing.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Test]
        public async Task HandleUpdateAsyncHandlerThrowsExceptionLogsError()
        {
            // Arrange
            var update = new Update { Id = 123, Message = new Message() };
            var cancellationToken = CancellationToken.None;
            var exception = new Exception("Handler failed");

            var handlerMock = new Mock<IUpdateHandlerCommand>();
            handlerMock.Setup(h => h.CanHandle(update)).Returns(true);
            handlerMock.Setup(h => h.HandleAsync(botClientMock.Object, update, cancellationToken))
                      .ThrowsAsync(exception);

            AddHandler(handlerMock);

            // Act
            await updateHandler.HandleUpdateAsync(botClientMock.Object, update, cancellationToken);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Error processing update")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that a debug log is recorded when an update is received.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Test]
        public async Task HandleUpdateAsyncLogsDebugOnReceipt()
        {
            // Arrange
            var update = new Update { Id = 123 };
            var cancellationToken = CancellationToken.None;

            // Act
            await updateHandler.HandleUpdateAsync(botClientMock.Object, update, cancellationToken);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("Received update") && v.ToString() !.Contains("123")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that the constructor logs the count of registered handlers during initialization.
        /// </summary>
        [Test]
        public void ConstructorWithHandlersLogsHandlerCount()
        {
            // Arrange
            var handlers = new List<IUpdateHandlerCommand>
            {
                new Mock<IUpdateHandlerCommand>().Object,
                new Mock<IUpdateHandlerCommand>().Object,
                new Mock<IUpdateHandlerCommand>().Object,
            };

            // Act
            var handler = new UpdateHandler(loggerMock.Object, handlers);

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() !.Contains("UpdateHandler initialized with 3 handlers")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        /// <summary>
        /// Tests that different types of updates are correctly routed to their corresponding handlers.
        /// Verifies that message updates go to message handlers, callback updates to callback handlers, etc.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Test]
        public async Task HandleUpdateAsyncMultipleHandlerTypesRoutesCorrectly()
        {
            // Arrange
            var messageUpdate = new Update { Id = 1, Message = new Message() };
            var callbackUpdate = new Update { Id = 2, CallbackQuery = new CallbackQuery() };
            var preCheckoutUpdate = new Update { Id = 3, PreCheckoutQuery = new PreCheckoutQuery() };
            var cancellationToken = CancellationToken.None;

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
            await updateHandler.HandleUpdateAsync(botClientMock.Object, messageUpdate, cancellationToken);
            messageHandlerMock.Verify(
                h => h.HandleAsync(botClientMock.Object, messageUpdate, cancellationToken),
                Times.Once);
            callbackHandlerMock.Verify(
                h => h.HandleAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Update>(), It.IsAny<CancellationToken>()),
                Times.Never);
            preCheckoutHandlerMock.Verify(
                h => h.HandleAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<Update>(), It.IsAny<CancellationToken>()),
                Times.Never);

            // Act & Assert - Callback update
            await updateHandler.HandleUpdateAsync(botClientMock.Object, callbackUpdate, cancellationToken);
            callbackHandlerMock.Verify(
                h => h.HandleAsync(botClientMock.Object, callbackUpdate, cancellationToken),
                Times.Once);

            // Act & Assert - PreCheckout update
            await updateHandler.HandleUpdateAsync(botClientMock.Object, preCheckoutUpdate, cancellationToken);
            preCheckoutHandlerMock.Verify(
                h => h.HandleAsync(botClientMock.Object, preCheckoutUpdate, cancellationToken),
                Times.Once);
        }

        /// <summary>
        /// Helper method to add a mock handler to the update handler instance.
        /// Recreates the update handler with the updated handler collection.
        /// </summary>
        /// <param name="handlerMock">The mock handler to add.</param>
        private void AddHandler(Mock<IUpdateHandlerCommand> handlerMock)
        {
            handlerMocks.Add(handlerMock);
            var handlers = handlerMocks.Select(m => m.Object).ToList();
            updateHandler = new UpdateHandler(loggerMock.Object, handlers);
        }
    }
}