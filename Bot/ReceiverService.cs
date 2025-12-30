using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace Bot;

/// <summary>
/// Receiver service acting as a bridge between polling and update processing.
/// </summary>
public class ReceiverService : IReceiverService
{
    private readonly ITelegramBotClient botClient;
    private readonly IUpdateHandler updateHandler;
    private readonly ILogger<ReceiverService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiverService"/> class.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="updateHandler">Handler for processing updates.</param>
    /// <param name="logger">Logger instance for tracking operations.</param>
    public ReceiverService(
        ITelegramBotClient botClient,
        IUpdateHandler updateHandler,
        ILogger<ReceiverService> logger)
    {
        this.botClient = botClient;
        this.updateHandler = updateHandler;
        this.logger = logger;

        this.logger.LogDebug("ReceiverService initialized");
    }

    /// <summary>
    /// Starts receiving updates from Telegram Bot API.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop receiving updates.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ReceiveAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = Array.Empty<UpdateType>(),
            DropPendingUpdates = true,
        };

        try
        {
            // Get bot information
            var me = await botClient.GetMe(stoppingToken);
            logger.LogInformation("Bot @{BotUsername} (ID: {BotId}) is starting to receive updates", me.Username, me.Id);

            // Start receiving updates (blocking call)
            logger.LogDebug("Starting to receive updates with configured options");

            await botClient.ReceiveAsync(
                updateHandler: updateHandler,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);

            logger.LogInformation("Bot @{BotUsername} stopped receiving updates", me.Username);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while receiving updates");
            throw;
        }
    }
}