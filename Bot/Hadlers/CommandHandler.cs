using Bot.Services;
using Data.Models;
using Microsoft.Extensions.Logging;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot.Handlers;

/// <summary>
/// Handles bot commands and routes them to appropriate handlers.
/// </summary>
public class CommandHandler
{
    private readonly ILogger<CommandHandler> logger;
    private readonly IGoalService goalService;
    private readonly KeyboardService keyboardService;
    private readonly AdminHandler adminHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    /// <param name="goalService">Service for goal-related operations.</param>
    /// <param name="keyboardService">Service for keyboard management.</param>
    /// <param name="adminHandler">Handler for administrative operations.</param>
    public CommandHandler(
        ILogger<CommandHandler> logger,
        IGoalService goalService,
        KeyboardService keyboardService,
        AdminHandler adminHandler)
    {
        this.logger = logger;
        this.goalService = goalService;
        this.keyboardService = keyboardService;
        this.adminHandler = adminHandler;

        this.logger.LogDebug("CommandHandler initialized successfully");
    }

    /// <summary>
    /// Processes and routes bot commands to appropriate handlers.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="message">The message containing the command.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.From?.Id == null)
        {
            logger.LogWarning("Received command from message with null user");
            return;
        }

        if (string.IsNullOrEmpty(message.Text))
        {
            logger.LogWarning("Received empty message text from user {UserId}", message.From.Id);
            return;
        }

        var chatId = message.Chat.Id;
        var userId = message.From.Id;
        var messageText = message.Text;

        logger.LogInformation("Processing command: {Command} from user {UserId}", messageText, userId);

        try
        {
            switch (messageText)
            {
                case "/start":
                case "🔄 Обновить":
                case "Обновить":
                    await HandleStartCommandAsync(botClient, message, cancellationToken);
                    break;
                case "/donate":
                case "💳 Пожертвовать":
                case "Пожертвовать":
                    await HandleDonateCommandAsync(botClient, chatId, cancellationToken);
                    break;
                case "/stats":
                case "📊 Статистика":
                case "Статистика":
                    await HandleStatsCommand(botClient, chatId, cancellationToken);
                    break;
                case "/addgoal":
                case "📝 Создать новую цель":
                case "Создать новую цель":
                    await HandleAddGoalCommandAsync(botClient, chatId, userId, cancellationToken);
                    break;
                default:
                    await HandleUnknownCommandAsync(botClient, message, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing command {Command} from user {UserId}", messageText, userId);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    /// <summary>
    /// Handles statistics command requests.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="chatId">Chat identifier.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task HandleStatsCommand(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing stats command for chat {ChatId}", chatId);

        try
        {
            var stats = await goalService.GetGoalStatsAsync();
            await botClient.SendMessage(
                chatId: chatId,
                text: stats,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);

            logger.LogDebug("Statistics sent successfully to chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting stats for chat {ChatId}", chatId);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    private async Task HandleStartCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From.Id;

        logger.LogDebug("Processing start command for user {UserId}", userId);

        try
        {
            var stats = await goalService.GetStartStats();
            var conclusion = $"🙏 Добро пожаловать в бота для сбора пожертвований! \n{stats} \n\nВыберите действие:";

            var isAdmin = await goalService.IsUserAdminAsync(userId);
            logger.LogDebug("User {UserId} admin status: {IsAdmin}", userId, isAdmin);

            var keyboard = isAdmin
                ? keyboardService.GetMainMenuKeyboardForAdmin()
                : keyboardService.GetMainMenuKeyboard();

            await botClient.SendMessage(
                chatId: chatId,
                text: conclusion,
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);

            logger.LogDebug("Start command processed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing start command for user {UserId}", userId);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    private async Task HandleDonateCommandAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing donate command for chat {ChatId}", chatId);

        try
        {
            var goal = await goalService.GetActiveGoalAsync();
            if (goal == null)
            {
                logger.LogWarning("No active goal found for donate command in chat {ChatId}", chatId);
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ В данный момент нет активных целей для пожертвований.",
                    cancellationToken: cancellationToken);
                return;
            }

            var keyboard = keyboardService.GetDonationAmountKeyboard();
            await botClient.SendMessage(
                chatId: chatId,
                text: $"💝 **Пожертвование: {goal.Title}** \n\nВыберите сумму пожертвования или введите свою:",
                parseMode: ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);

            logger.LogDebug("Donate command processed successfully for chat {ChatId} with goal {GoalTitle}", chatId, goal.Title);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing donate command for chat {ChatId}", chatId);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    private async Task HandleAddGoalCommandAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        logger.LogDebug("Processing add goal command for user {UserId}", userId);

        try
        {
            if (await goalService.IsUserAdminAsync(userId))
            {
                logger.LogInformation("Admin user {UserId} starting goal creation", userId);
                await adminHandler.StartGoalCreationAsync(botClient, chatId, userId, cancellationToken);
            }
            else
            {
                logger.LogWarning("Non-admin user {UserId} attempted to create goal", userId);
                await adminHandler.HandleNotAdmin(botClient, chatId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing add goal command for user {UserId}", userId);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    private async Task HandleUnknownCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var userId = message.From.Id;

        logger.LogWarning("Unknown command from user {UserId}: {CommandText}", userId, message.Text);

        try
        {
            var isAdmin = await goalService.IsUserAdminAsync(userId);
            var responseText = isAdmin
                ? "🤔 Неизвестная команда \n\n Доступные команды: \n• Нажмите 📊 Статистика для просмотра прогресса \n• Нажмите 📝 Создать новую цель"
                : "🤔 Неизвестная команда \n\n Доступные команды: \n• Нажмите 📊 Статистика для просмотра прогресса \n• Нажмите 💳 Пожертвовать для помощи проекту";

            await botClient.SendMessage(
                chatId: chatId,
                text: responseText,
                cancellationToken: cancellationToken);

            logger.LogDebug("Unknown command response sent to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending unknown command response to user {UserId}", userId);
        }
    }

    private async Task SendErrorMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        try
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "❌ Не удалось выполнить команду. Попробуйте позже.",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send error message to chat {ChatId}", chatId);
        }
    }
}