using Bot.Services;
using Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;

namespace Bot.Handlers;

/// <summary>
/// Handles payment-related operations including donation invoices and payment processing.
/// </summary>
public class PaymentHandler
{
    private readonly ILogger<PaymentHandler> logger;
    private readonly IGoalService goalService;
    private readonly IDonationService donationService;
    private readonly UserStateService userStateService;
    private readonly BotConfig botConfig;

    private const int MinimumDonationAmount = 60;
    private const int MaximumDonationAmount = 100000;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentHandler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    /// <param name="goalService">Service for goal-related operations.</param>
    /// <param name="donationService">Service for donation processing.</param>
    /// <param name="userStateService">Service for managing user state.</param>
    /// <param name="botConfig">Bot configuration options.</param>
    public PaymentHandler(
        ILogger<PaymentHandler> logger,
        IGoalService goalService,
        IDonationService donationService,
        UserStateService userStateService,
        IOptions<BotConfig> botConfig)
    {
        this.logger = logger;
        this.goalService = goalService;
        this.donationService = donationService;
        this.userStateService = userStateService;
        this.botConfig = botConfig.Value;

        this.logger.LogDebug("PaymentHandler initialized successfully");
    }

    /// <summary>
    /// Processes custom donation amount input from users.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="message">The message containing the custom amount.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task HandleCustomAmountInputAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.From?.Id == null)
        {
            logger.LogWarning("Received custom amount input from null user");
            return;
        }

        var userId = message.From.Id;
        var chatId = message.Chat.Id;

        logger.LogDebug("Processing custom amount input from user {UserId}", userId);

        userStateService.RemoveWaitingForAmount(userId);

        if (string.IsNullOrEmpty(message.Text))
        {
            logger.LogWarning("User {UserId} sent empty custom amount", userId);
            await SendInvalidAmountMessageAsync(botClient, chatId, cancellationToken);
            return;
        }

        if (!int.TryParse(message.Text, out int amount) || amount <= 0)
        {
            logger.LogWarning("User {UserId} sent invalid custom amount: {AmountText}", userId, message.Text);
            await SendInvalidAmountMessageAsync(botClient, chatId, cancellationToken);
            return;
        }

        await CreateDonationInvoice(botClient, chatId, userId, amount, cancellationToken);
    }

    /// <summary>
    /// Creates a donation invoice for the specified amount.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="chatId">Chat identifier.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="amountRub">Donation amount in rubles.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task CreateDonationInvoice(ITelegramBotClient botClient, long chatId, long userId, int amountRub, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating donation invoice for user {UserId} with amount {AmountRub} RUB", userId, amountRub);

        try
        {
            var goal = await goalService.GetActiveGoalAsync();
            if (goal == null)
            {
                logger.LogWarning("No active goal found for donation from user {UserId}", userId);
                await botClient.SendMessage(chatId, "❌ В данный момент нет активных целей для пожертвований.", cancellationToken: cancellationToken);
                return;
            }

            if (!ValidateDonationAmount(amountRub))
            {
                logger.LogWarning("User {UserId} provided invalid donation amount: {AmountRub}", userId, amountRub);
                await SendAmountValidationMessageAsync(botClient, chatId, cancellationToken);
                return;
            }

            await SendDonationInvoiceAsync(botClient, chatId, userId, amountRub, goal, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating donation invoice for user {UserId}", userId);
            await SendErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    /// <summary>
    /// Processes successful payments and updates donation records.
    /// </summary>
    /// <param name="botClient">Telegram bot client instance.</param>
    /// <param name="message">The message containing successful payment information.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task HandleSuccessfulPaymentAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (message.SuccessfulPayment == null || message.From == null)
        {
            logger.LogWarning("Received successful payment with null payment or user information");
            return;
        }

        var payment = message.SuccessfulPayment;
        var user = message.From;
        var chatId = message.Chat.Id;

        logger.LogInformation("Processing successful payment from user {UserId}, charge ID: {ChargeId}", user.Id, payment.TelegramPaymentChargeId);

        try
        {
            await DeleteInvoiceMessageAsync(botClient, chatId, message.MessageId);

            var amount = payment.TotalAmount / 100m;
            var success = await donationService.ProcessDonationAsync(user.Id, amount, payment.Currency, payment.TelegramPaymentChargeId);

            if (success)
            {
                await SendThankYouMessageAsync(botClient, chatId, amount, payment.Currency, cancellationToken);
                logger.LogInformation("Successfully processed donation from user {UserId}, amount: {Amount} {Currency}", user.Id, amount, payment.Currency);
            }
            else
            {
                await SendDonationProcessingErrorMessageAsync(botClient, chatId, cancellationToken);
                logger.LogError("Failed to process donation from user {UserId}, charge ID: {ChargeId}", user.Id, payment.TelegramPaymentChargeId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling successful payment from user {UserId}", user.Id);
            await SendPaymentProcessingErrorMessageAsync(botClient, chatId, cancellationToken);
        }
    }

    private bool ValidateDonationAmount(int amountRub)
    {
        if (amountRub > MaximumDonationAmount)
        {
            logger.LogWarning("Donation amount {AmountRub} exceeds maximum limit", amountRub);
            return false;
        }

        if (amountRub < MinimumDonationAmount)
        {
            logger.LogWarning("Donation amount {AmountRub} below minimum limit", amountRub);
            return false;
        }

        return true;
    }

    private async Task SendDonationInvoiceAsync(ITelegramBotClient botClient, long chatId, long userId, int amountRub, Data.Models.DonationGoal goal, CancellationToken cancellationToken)
    {
        var amountKopecks = amountRub * 100;
        var prices = new[] { new LabeledPrice("Пожертвование", amountKopecks) };
        var payload = $"donation_{goal.Id}_{userId}_{DateTime.UtcNow.Ticks}";

        if (string.IsNullOrEmpty(botConfig.PaymentProviderToken))
        {
            logger.LogError("Payment provider token is not configured");
            throw new InvalidOperationException("Payment provider token is not configured");
        }

        await botClient.SendInvoice(
            chatId: chatId,
            title: $"Пожертвование: {goal.Title}",
            description: $"Пожертвование на сумму {amountRub} руб.",
            payload: payload,
            providerToken: botConfig.PaymentProviderToken,
            currency: "RUB",
            prices: prices,
            cancellationToken: cancellationToken);

        logger.LogInformation("Donation invoice created for user {UserId}, amount: {AmountRub} RUB, goal: {GoalTitle}", userId, amountRub, goal.Title);
    }

    private async Task DeleteInvoiceMessageAsync(ITelegramBotClient botClient, long chatId, int messageId)
    {
        try
        {
            await botClient.DeleteMessage(chatId, messageId);
            logger.LogDebug("Invoice message {MessageId} deleted successfully", messageId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete invoice message {MessageId}", messageId);
        }
    }

    private async Task SendThankYouMessageAsync(ITelegramBotClient botClient, long chatId, decimal amount, string currency, CancellationToken cancellationToken)
    {
        var stats = await goalService.GetStartStats();
        await botClient.SendMessage(
            chatId: chatId,
            text: $"✅ **Спасибо за ваше пожертвование!** \n\n💝 Вы пожертвовали: {amount:N2} {currency} \n{stats} \n\nВаша поддержка очень важна для нас!",
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }

    private async Task SendInvalidAmountMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: "❌ Пожалуйста, введите корректную сумму в рублях (только цифры)",
            cancellationToken: cancellationToken);
    }

    private async Task SendAmountValidationMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: $"❌ Сумма пожертвования должна быть от {MinimumDonationAmount} до {MaximumDonationAmount} руб.",
            cancellationToken: cancellationToken);
    }

    private async Task SendErrorMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: "❌ Произошла ошибка при создании платежа. Попробуйте позже.",
            cancellationToken: cancellationToken);
    }

    private async Task SendDonationProcessingErrorMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: "❌ Произошла ошибка при обработке вашего платежа. Мы уже работаем над этим.",
            cancellationToken: cancellationToken);
    }

    private async Task SendPaymentProcessingErrorMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: "❌ Произошла ошибка при обработке платежа. Мы уже работаем над этим.",
            cancellationToken: cancellationToken);
    }
}