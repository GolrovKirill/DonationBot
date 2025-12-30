using Bot.Services;
using Microsoft.Extensions.Logging;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;

namespace Bot.Handlers;

/// <summary>
/// Handles pre-checkout queries to validate payment requests before processing.
/// </summary>
public class PreCheckoutQueryHandler : IUpdateHandlerCommand
{
    private readonly ILogger<PreCheckoutQueryHandler> logger;
    private readonly IGoalService goalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreCheckoutQueryHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    /// <param name="goalService">Service for goal-related operations.</param>
    public PreCheckoutQueryHandler(
        ILogger<PreCheckoutQueryHandler> logger,
        IGoalService goalService)
    {
        this.logger = logger;
        this.goalService = goalService;

        this.logger.LogDebug("PreCheckoutQueryHandler initialized successfully");
    }

    /// <summary>
    /// Determines whether this handler can process the update.
    /// </summary>
    /// <param name="update">The update to check.</param>
    /// <returns>True if the update contains a pre-checkout query; otherwise, false.</returns>
    public bool CanHandle(Update update) => update.PreCheckoutQuery != null;

    /// <summary>
    /// Processes pre-checkout queries to validate payment requests.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="update">The update containing the pre-checkout query.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var preCheckoutQuery = update.PreCheckoutQuery!;

        if (preCheckoutQuery.From == null)
        {
            logger.LogWarning("Received pre-checkout query with null user information");
            return;
        }

        logger.LogInformation(
            "Processing pre-checkout query from user {UserId} with payload {InvoicePayload}", preCheckoutQuery.From.Id, preCheckoutQuery.InvoicePayload);

        try
        {
            var goal = await goalService.GetActiveGoalAsync();

            if (goal != null)
            {
                await botClient.AnswerPreCheckoutQuery(
                    preCheckoutQueryId: preCheckoutQuery.Id,
                    cancellationToken: cancellationToken);

                logger.LogInformation(
                    "Approved pre-checkout query for user {UserId}, payload: {InvoicePayload}, amount: {Amount} {Currency}",
                    preCheckoutQuery.From.Id, preCheckoutQuery.InvoicePayload, preCheckoutQuery.TotalAmount / 100m, preCheckoutQuery.Currency);
            }
            else
            {
                logger.LogWarning(
                    "Rejected pre-checkout query from user {UserId} - no active goal found",
                    preCheckoutQuery.From.Id);

                await botClient.AnswerPreCheckoutQuery(
                    preCheckoutQueryId: preCheckoutQuery.Id,
                    errorMessage: "В данный момент нет активных целей для пожертвований",
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error handling pre-checkout query from user {UserId}",
                preCheckoutQuery.From.Id);

            await SafeAnswerPreCheckoutQueryAsync(
                botClient,
                preCheckoutQuery.Id,
                "Произошла ошибка при обработке платежа",
                cancellationToken);
        }
    }

    private async Task SafeAnswerPreCheckoutQueryAsync(
        ITelegramBotClient botClient,
        string preCheckoutQueryId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await botClient.AnswerPreCheckoutQuery(
                preCheckoutQueryId: preCheckoutQueryId,
                errorMessage: errorMessage,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to answer pre-checkout query {PreCheckoutQueryId}", preCheckoutQueryId);
        }
    }
}