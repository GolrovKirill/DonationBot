using Bot.Services;
using Microsoft.Extensions.Logging;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using static Bot.Services.AdminStateService;

namespace Bot.Handlers;

/// <summary>
/// Handles administrative operations for goal management including creation and validation.
/// </summary>
public class AdminHandler
{
    private readonly ILogger<AdminHandler> logger;
    private readonly IGoalService goalService;
    private readonly AdminStateService adminStateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    /// <param name="goalService">Service for goal-related operations.</param>
    /// <param name="adminStateService">Service for managing admin state.</param>
    public AdminHandler(
        ILogger<AdminHandler> logger,
        IGoalService goalService,
        AdminStateService adminStateService)
    {
        this.logger = logger;
        this.goalService = goalService;
        this.adminStateService = adminStateService;

        this.logger.LogDebug("AdminHandler initialized successfully");
    }

    /// <summary>
    /// Processes goal creation workflow for administrators.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="message">Incoming message from user.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task HandleAdminGoalCreationAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.From?.Id == null)
        {
            logger.LogWarning("Received message with null user ID");
            return;
        }

        var userId = message.From.Id;
        var chatId = message.Chat.Id;
        var state = adminStateService.GetState(userId);

        if (state == null)
        {
            logger.LogWarning("No admin state found for user {UserId}", userId);
            return;
        }

        if (string.IsNullOrEmpty(message.Text))
        {
            logger.LogWarning("Received empty message text from user {UserId}", userId);
            return;
        }

        try
        {
            switch (state.CurrentStep)
            {
                case AdminStateService.AdminGoalStep.WaitingForTitle:
                    await ProcessTitleStepAsync(botClient, userId, chatId, message.Text, cancellationToken);
                    break;

                case AdminStateService.AdminGoalStep.WaitingForDescription:
                    await ProcessDescriptionStepAsync(botClient, userId, chatId, message.Text, cancellationToken);
                    break;

                case AdminStateService.AdminGoalStep.WaitingForAmount:
                    await ProcessAmountStepAsync(botClient, userId, chatId, message.Text, state, cancellationToken);
                    break;

                default:
                    logger.LogWarning("Unknown admin step {CurrentStep} for user {UserId}", state.CurrentStep, userId);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing admin goal creation for user {UserId} at step {CurrentStep}", userId, state.CurrentStep);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    /// <summary>
    /// Initiates the goal creation process for administrators.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="chatId">Chat identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task StartGoalCreationAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting goal creation process for admin user {UserId}", userId);

        try
        {
            adminStateService.StartGoalCreation(userId, chatId);
            await botClient.SendMessage(chatId, "🎯 Введите название новой цели:", cancellationToken: cancellationToken);

            logger.LogDebug("Goal creation started successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start goal creation for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Handles unauthorized admin access attempts.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="chatId">Chat identifier.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task HandleNotAdmin(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        logger.LogWarning("Non-admin access attempt detected for chat {ChatId}", chatId);

        await botClient.SendMessage(chatId, "❌ Вы не являетесь админом", cancellationToken: cancellationToken);
    }

    private async Task ProcessTitleStepAsync(ITelegramBotClient botClient, long userId, long chatId, string title, CancellationToken cancellationToken)
    {
        if (title.Length >= 255)
        {
            logger.LogWarning("User {UserId} provided title that exceeds length limit: {TitleLength}", userId, title.Length);

            adminStateService.CancelGoalCreation(userId);
            await botClient.SendMessage(chatId, "❌ Слишком длинное название цели \n Попробуйте создать цель заново:", cancellationToken: cancellationToken);
            return;
        }

        logger.LogDebug("Setting title for user {UserId}", userId);
        adminStateService.SetTitle(userId, title);
        await botClient.SendMessage(chatId, "📝 Введите описание цели:", cancellationToken: cancellationToken);
    }

    private async Task ProcessDescriptionStepAsync(ITelegramBotClient botClient, long userId, long chatId, string description, CancellationToken cancellationToken)
    {
        logger.LogDebug("Setting description for user {UserId}", userId);

        adminStateService.SetDescription(userId, description);
        await botClient.SendMessage(chatId, "💰 Введите целевую сумму в рублях:", cancellationToken: cancellationToken);
    }

    private async Task ProcessAmountStepAsync(ITelegramBotClient botClient, long userId, long chatId, string amountText, AdminGoalCreationState state, CancellationToken cancellationToken)
    {
        if (!decimal.TryParse(amountText, out decimal amount) || amount <= 0 || amount >= 100000000)
        {
            logger.LogWarning("User {UserId} provided invalid amount: {AmountText}", userId, amountText);

            adminStateService.CancelGoalCreation(userId);
            await botClient.SendMessage(chatId, "❌ Введите корректную сумму до 99999999 \n Попробуйте создать цель заново:", cancellationToken: cancellationToken);
            return;
        }

        try
        {
            logger.LogInformation("Creating goal for user {UserId} with amount {Amount}", userId, amount);

            var goal = await goalService.CreateGoalAsync(state.Title!, state.Description!, amount);
            adminStateService.CancelGoalCreation(userId);

            await botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Цель создана!\n🎯 {goal.Title}\n💫 Описание: {goal.Description} \n💰 Сумма: {amount}₽",
                cancellationToken: cancellationToken);

            logger.LogInformation("Goal created successfully with ID {GoalId} for user {UserId}", goal.Id, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create goal in database for user {UserId}", userId);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    private async Task SendErrorMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken) =>
        await botClient.SendMessage(
            chatId,
            "❌ Произошла ошибка при создании цели. Пожалуйста, попробуйте позже.",
            cancellationToken: cancellationToken);
}