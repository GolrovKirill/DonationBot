// <copyright file="KeyboardService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Services;

/// <summary>
/// Service for creating and managing Telegram bot keyboards and markup.
/// </summary>
public class KeyboardService
{
    private readonly ILogger<KeyboardService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    public KeyboardService(ILogger<KeyboardService> logger)
    {
        this.logger = logger;
        this.logger.LogDebug("KeyboardService initialized");
    }

    /// <summary>
    /// Creates the main menu keyboard for regular users.
    /// </summary>
    /// <returns>Configured reply keyboard markup for main menu.</returns>
    public virtual ReplyKeyboardMarkup GetMainMenuKeyboard()
    {
        logger.LogDebug("Creating main menu keyboard for regular user");

        return new ReplyKeyboardMarkup(
        [
            [new KeyboardButton("📊 Статистика"), new KeyboardButton("💳 Пожертвовать")],
            [new KeyboardButton("🔄 Обновить")],
        ])
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
        };
    }

    /// <summary>
    /// Creates the main menu keyboard for admin users with administrative options.
    /// </summary>
    /// <returns>Configured reply keyboard markup for admin main menu.</returns>
    public virtual ReplyKeyboardMarkup GetMainMenuKeyboardForAdmin()
    {
        logger.LogDebug("Creating main menu keyboard for admin user");

        return new ReplyKeyboardMarkup(
        [
            [new KeyboardButton("📊 Статистика"), new KeyboardButton("📝 Создать новую цель")],
            [new KeyboardButton("🔄 Обновить")],
        ])
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
        };
    }

    /// <summary>
    /// Creates an inline keyboard for donation amount selection.
    /// </summary>
    /// <returns>Configured inline keyboard markup for donation amounts.</returns>
    public virtual InlineKeyboardMarkup GetDonationAmountKeyboard()
    {
        logger.LogDebug("Creating donation amount inline keyboard");

        try
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("100 ₽", "donate_100"),
                    InlineKeyboardButton.WithCallbackData("500 ₽", "donate_500"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("1000 ₽", "donate_1000"),
                    InlineKeyboardButton.WithCallbackData("5000 ₽", "donate_5000"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💎 Другая сумма", "enter_custom_amount"),
                },
            });

            logger.LogDebug("Donation amount keyboard created successfully");
            return keyboard;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating donation amount keyboard");
            throw;
        }
    }

    /// <summary>
    /// Creates a simple inline keyboard with a single back button.
    /// </summary>
    /// <param name="callbackData">The callback data for the back button.</param>
    /// <returns>Configured inline keyboard markup with back button.</returns>
    public InlineKeyboardMarkup GetBackButtonKeyboard(string callbackData = "back_to_main")
    {
        logger.LogDebug("Creating back button keyboard with callback data: {CallbackData}", callbackData);

        return new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("⬅️ Назад", callbackData));
    }

    /// <summary>
    /// Creates a confirmation inline keyboard for important actions.
    /// </summary>
    /// <param name="confirmCallbackData">Callback data for confirm action.</param>
    /// <param name="cancelCallbackData">Callback data for cancel action.</param>
    /// <returns>Configured inline keyboard markup for confirmation.</returns>
    public InlineKeyboardMarkup GetConfirmationKeyboard(string confirmCallbackData = "confirm", string cancelCallbackData = "cancel")
    {
        logger.LogDebug("Creating confirmation keyboard with confirm: {Confirm}, cancel: {Cancel}", confirmCallbackData, cancelCallbackData);

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", confirmCallbackData),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", cancelCallbackData),
            },
        });
    }
}