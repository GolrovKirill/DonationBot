using Bot.Handlers;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace Bot;

/// <summary>
/// Handles incoming updates from Telegram Bot API and routes them to appropriate handlers.
/// </summary>
public class UpdateHandler : IUpdateHandler
{
    private readonly ILogger<UpdateHandler> logger;
    private readonly IEnumerable<IUpdateHandlerCommand> handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    /// <param name="handlers">Collection of update handler commands.</param>
    public UpdateHandler(
        ILogger<UpdateHandler> logger,
        IEnumerable<IUpdateHandlerCommand> handlers)
    {
        this.logger = logger;
        this.handlers = handlers;

        this.logger.LogDebug("UpdateHandler initialized with {HandlerCount} handlers", handlers.Count());
    }

    /// <summary>
    /// Processes incoming updates by routing them to appropriate handlers.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="update">The update to process.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        logger.LogDebug("Received update {UpdateId} of type {UpdateType}", update.Id, update.Type);

        try
        {
            var handler = handlers.FirstOrDefault(h => h.CanHandle(update));

            if (handler != null)
            {
                logger.LogDebug("Routing update {UpdateId} to handler {HandlerType}", update.Id, handler.GetType().Name);

                await handler.HandleAsync(botClient, update, cancellationToken);
            }
            else
            {
                logger.LogWarning("No handler found for update {UpdateId} of type {UpdateType}", update.Id, update.Type);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing update {UpdateId} of type {UpdateType}", update.Id, update.Type);
        }
    }

    /// <summary>
    /// Handles errors that occur during update processing.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="errorSource">The source of the error.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource errorSource, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
            RequestException requestException
                => $"Request Error: {requestException.Message}",
            _ => exception.Message
        };

        logger.LogError(
            exception,
            "Error occurred from source {ErrorSource}. Message: {ErrorMessage}", errorSource, errorMessage);

        // Implement retry delay for network-related errors
        if (exception is HttpRequestException or RequestException)
        {
            logger.LogInformation("Network error detected, waiting 1 second before retry");

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                logger.LogDebug("Retry delay was cancelled");
            }
        }

        // Log specific API error codes for monitoring
        if (exception is ApiRequestException apiEx)
        {
            LogApiErrorDetails(apiEx);
        }
    }

    /// <summary>
    /// Logs detailed information about Telegram API errors for monitoring and debugging.
    /// </summary>
    /// <param name="apiException">The API request exception.</param>
    private void LogApiErrorDetails(ApiRequestException apiException)
    {
        var logLevel = apiException.ErrorCode switch
        {
            400 => LogLevel.Warning, // Bad Request
            401 => LogLevel.Critical, // Unauthorized
            403 => LogLevel.Warning, // Forbidden
            404 => LogLevel.Information, // Not Found
            429 => LogLevel.Warning, // Too Many Requests
            500 => LogLevel.Error, // Internal Server Error
            _ => LogLevel.Error
        };

        logger.Log(logLevel, apiException, "Telegram API Error {ErrorCode}: {ErrorMessage}", apiException.ErrorCode, apiException.Message);
    }
}