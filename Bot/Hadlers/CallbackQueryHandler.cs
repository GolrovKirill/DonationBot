using Bot.Services;
using Microsoft.Extensions.Logging;
using Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace Bot.Handlers;

/// <summary>
/// Handles callback queries from inline keyboards in Telegram bot.
/// </summary>
public class CallbackQueryHandler : IUpdateHandlerCommand
{
    private readonly ILogger<CallbackQueryHandler> logger;
    private readonly PaymentHandler paymentHandler;
    private readonly CommandHandler commandHandler;
    private readonly UserStateService userStateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackQueryHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    /// <param name="paymentHandler">Handler for payment-related operations.</param>
    /// <param name="commandHandler">Handler for command processing.</param>
    /// <param name="userStateService">Service for managing user state.</param>
    public CallbackQueryHandler(
        ILogger<CallbackQueryHandler> logger,
        PaymentHandler paymentHandler,
        CommandHandler commandHandler,
        UserStateService userStateService)
    {
        this.logger = logger;
        this.paymentHandler = paymentHandler;
        this.commandHandler = commandHandler;
        this.userStateService = userStateService;

        this.logger.LogDebug("CallbackQueryHandler initialized successfully");
    }

    /// <summary>
    /// Determines whether this handler can process the update.
    /// </summary>
    /// <param name="update">The update to check.</param>
    /// <returns>True if the update contains a callback query; otherwise, false.</returns>
    public bool CanHandle(Update update) => update.CallbackQuery != null;

    /// <summary>
    /// Processes callback query updates from inline keyboards.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="update">The update containing callback query.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var callbackQuery = update.CallbackQuery!;
        logger.LogInformation("Received inline keyboard callback from user: {UserId}", callbackQuery.From.Id);

        if (callbackQuery.Message?.Chat == null)
        {
            logger.LogWarning("Callback query from user {UserId} has no chat information", callbackQuery.From.Id);
            return;
        }

        try
        {
            var callbackData = callbackQuery.Data;
            var chatId = callbackQuery.Message.Chat.Id;
            var userId = callbackQuery.From.Id;

            if (string.IsNullOrEmpty(callbackData))
            {
                logger.LogWarning("Empty callback data received from user {UserId}", userId);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Пустая команда", cancellationToken: cancellationToken);
                return;
            }

            await ProcessCallbackDataAsync(botClient, callbackQuery, callbackData, chatId, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling callback query from user {UserId}", callbackQuery.From.Id);
            await SafeAnswerCallbackQueryAsync(botClient, callbackQuery.Id, "❌ Произошла ошибка", cancellationToken);
        }
    }

    private async Task ProcessCallbackDataAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, string callbackData, long chatId, long userId, CancellationToken cancellationToken)
    {
        switch (callbackData)
        {
            case "enter_custom_amount":
                await HandleCustomAmountAsync(botClient, callbackQuery, chatId, userId, cancellationToken);
                break;

            case "donate_100":
            case "donate_500":
            case "donate_1000":
            case "donate_5000":
                await HandlePredefinedDonationAsync(botClient, callbackQuery, callbackData, chatId, userId, cancellationToken);
                break;

            case "show_stats":
                await HandleShowStatsAsync(botClient, callbackQuery, chatId, cancellationToken);
                break;

            default:
                logger.LogWarning("Unknown callback data: {CallbackData} from user {UserId}", callbackData, userId);
                await botClient.AnswerCallbackQuery(callbackQuery.Id, $"❌ Неизвестная команда: {callbackData}", cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleCustomAmountAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, long chatId, long userId, CancellationToken cancellationToken)
    {
        logger.LogDebug("User {UserId} selected custom amount donation", userId);

        userStateService.SetWaitingForAmount(userId, chatId);
        await botClient.AnswerCallbackQuery(callbackQuery.Id, "Введите сумму пожертвования в рублях", cancellationToken: cancellationToken);
        await botClient.SendMessage(chatId, "💎 Введите сумму пожертвования в рублях:", cancellationToken: cancellationToken);

        logger.LogDebug("Custom amount prompt sent to user {UserId}", userId);
    }

    private async Task HandlePredefinedDonationAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, string callbackData, long chatId, long userId, CancellationToken cancellationToken)
    {
        try
        {
            var amount = int.Parse(callbackData.Split('_')[1]);
            logger.LogInformation("User {UserId} selected predefined donation amount: {Amount}", userId, amount);

            await paymentHandler.CreateDonationInvoice(botClient, chatId, userId, amount, cancellationToken);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

            logger.LogDebug("Donation invoice created for user {UserId} with amount {Amount}", userId, amount);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "Failed to parse donation amount from callback data: {CallbackData}", callbackData);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Ошибка обработки суммы", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create donation invoice for user {UserId}", userId);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Ошибка создания платежа", cancellationToken: cancellationToken);
        }
    }

    private async Task HandleShowStatsAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, long chatId, CancellationToken cancellationToken)
    {
        logger.LogDebug("User {UserId} requested statistics", callbackQuery.From.Id);

        try
        {
            await commandHandler.HandleStatsCommand(botClient, chatId, cancellationToken);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

            logger.LogDebug("Statistics sent to user {UserId}", callbackQuery.From.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show statistics for user {UserId}", callbackQuery.From.Id);
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "❌ Ошибка загрузки статистики", cancellationToken: cancellationToken);
        }
    }

    private async Task SafeAnswerCallbackQueryAsync(ITelegramBotClient botClient, string callbackQueryId, string message, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.AnswerCallbackQuery(callbackQueryId, message, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to answer callback query {CallbackQueryId}", callbackQueryId);
        }
    }
}