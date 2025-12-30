using Bot.Services;
using Data.Models;
using Microsoft.Extensions.Logging;
using Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot.Handlers;

/// <summary>
/// Handles incoming messages and routes them to appropriate processors based on message type and user state.
/// </summary>
public class MessageHandler : IUpdateHandlerCommand
{
    private readonly ILogger<MessageHandler> logger;
    private readonly IDonationService donationService;
    private readonly IGoalService goalService;
    private readonly CommandHandler commandHandler;
    private readonly PaymentHandler paymentHandler;
    private readonly UserStateService userStateService;
    private readonly AdminHandler adminHandler;
    private readonly AdminStateService adminStateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    /// <param name="donationService">Service for donation-related operations.</param>
    /// <param name="goalService">Service for goal-related operations.</param>
    /// <param name="commandHandler">Handler for command processing.</param>
    /// <param name="paymentHandler">Handler for payment processing.</param>
    /// <param name="userStateService">Service for managing user state.</param>
    /// <param name="adminHandler">Handler for administrative operations.</param>
    /// <param name="adminStateService">Service for managing admin state.</param>
    public MessageHandler(
        ILogger<MessageHandler> logger,
        IDonationService donationService,
        IGoalService goalService,
        CommandHandler commandHandler,
        PaymentHandler paymentHandler,
        UserStateService userStateService,
        AdminHandler adminHandler,
        AdminStateService adminStateService)
    {
        this.logger = logger;
        this.donationService = donationService;
        this.goalService = goalService;
        this.commandHandler = commandHandler;
        this.paymentHandler = paymentHandler;
        this.userStateService = userStateService;
        this.adminHandler = adminHandler;
        this.adminStateService = adminStateService;

        this.logger.LogDebug("MessageHandler initialized successfully");
    }

    /// <summary>
    /// Determines whether this handler can process the update.
    /// </summary>
    /// <param name="update">The update to check.</param>
    /// <returns>True if the update contains a message; otherwise, false.</returns>
    public bool CanHandle(Update update) => update.Message != null;

    /// <summary>
    /// Processes incoming messages and routes them based on message type and user state.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="update">The update containing the message.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message!;

        if (message.From is null)
        {
            logger.LogWarning("Received message with null user information");
            return;
        }

        var userId = message.From.Id;
        var chatId = message.Chat.Id;

        logger.LogDebug("Processing message from user {UserId} in chat {ChatId}", userId, chatId);

        try
        {
            await RegisterOrUpdateUserAsync(message);

            if (await ProcessSpecialMessageTypesAsync(botClient, message, cancellationToken))
            {
                return;
            }

            if (await ProcessUserStateBasedMessagesAsync(botClient, message, cancellationToken))
            {
                return;
            }

            await ProcessTextMessageAsync(botClient, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message from user {UserId}", userId);
        }
    }

    private async Task RegisterOrUpdateUserAsync(Message message)
    {
        try
        {
            await donationService.GetOrCreateUserAsync(
                message.From.Id,
                message.From.Username,
                message.From.FirstName,
                message.From.LastName);

            logger.LogDebug("User {UserId} registered/updated successfully", message.From.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register/update user {UserId}", message.From.Id);
        }
    }

    private async Task<bool> ProcessSpecialMessageTypesAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.SuccessfulPayment != null)
        {
            logger.LogInformation("Processing successful payment from user {UserId}", message.From.Id);
            await paymentHandler.HandleSuccessfulPaymentAsync(botClient, message, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task<bool> ProcessUserStateBasedMessagesAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var userId = message.From.Id;
        var chatId = message.Chat.Id;

        if (userStateService.IsWaitingForAmount(userId, chatId))
        {
            logger.LogDebug("User {UserId} is in waiting for amount state", userId);
            await paymentHandler.HandleCustomAmountInputAsync(botClient, message, cancellationToken);
            return true;
        }

        var isAdmin = await goalService.IsUserAdminAsync(userId);
        if (isAdmin && adminStateService.IsUserCreatingGoal(userId))
        {
            logger.LogDebug("Admin user {UserId} is in goal creation state", userId);
            await adminHandler.HandleAdminGoalCreationAsync(botClient, message, cancellationToken);
            return true;
        }

        return false;
    }

    private async Task ProcessTextMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            logger.LogDebug("Received non-text message from user {UserId}", message.From.Id);
            return;
        }

        logger.LogInformation("Processing text message from user {UserId}: {MessageText}", message.From.Id, message.Text);

        try
        {
            await commandHandler.HandleCommandAsync(botClient, message, cancellationToken);
            logger.LogDebug("Text message processed successfully for user {UserId}", message.From.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing text message from user {UserId}", message.From.Id);
        }
    }
}