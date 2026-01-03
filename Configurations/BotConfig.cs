// <copyright file="BotConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Configurations;

/// <summary>
/// Represents the configuration settings for the bot application.
/// </summary>
public class BotConfig
{
    /// <summary>
    /// Gets or sets the bot token used for authenticating with the Telegram Bot API.
    /// </summary>
    /// <value>
    /// The bot token string. This should be kept secure and not exposed publicly.
    /// </value>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the payment provider token for processing donations.
    /// </summary>
    /// <value>
    /// The payment provider token string. Used for integrating with payment systems.
    /// </value>
    public string PaymentProviderToken { get; set; } = string.Empty;
}
