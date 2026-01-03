// <copyright file="Donation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Data.Models;

/// <summary>
/// Represents a donation made by a user towards a specific goal.
/// </summary>
public class Donation
{
    /// <summary>
    /// Gets or sets the unique identifier for the donation.
    /// </summary>
    /// <value>
    /// The donation identifier.
    /// </value>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the Telegram user identifier of the donor.
    /// </summary>
    /// <value>
    /// The Telegram user identifier.
    /// </value>
    public long UserTelegramId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the goal this donation is for.
    /// </summary>
    /// <value>
    /// The goal identifier.
    /// </value>
    public int GoalId { get; set; }

    /// <summary>
    /// Gets or sets the amount of the donation.
    /// </summary>
    /// <value>
    /// The donation amount in the specified currency.
    /// </value>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of the donation. Default is "RUB" (Russian Ruble).
    /// </summary>
    /// <value>
    /// The 3-letter currency code (ISO 4217).
    /// </value>
    public string Currency { get; set; } = "RUB";

    /// <summary>
    /// Gets or sets the payment provider's transaction identifier.
    /// </summary>
    /// <value>
    /// The payment provider's unique transaction identifier, or null if not available.
    /// </value>
    public string? ProviderPaymentId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the donation. Default is "pending".
    /// </summary>
    /// <value>
    /// The donation status (e.g., "pending", "completed", "failed", "refunded").
    /// </value>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets the date and time when the donation was created.
    /// </summary>
    /// <value>
    /// The creation timestamp in UTC.
    /// </value>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}