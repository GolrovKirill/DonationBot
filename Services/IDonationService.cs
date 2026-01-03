// <copyright file="IDonationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Data.Models;

namespace Services;

/// <summary>
/// Service for handling donation-related operations and user management.
/// </summary>
public interface IDonationService
{
    /// <summary>
    /// Retrieves an existing user or creates a new one if not found.
    /// </summary>
    /// <param name="telegramId">The Telegram user identifier.</param>
    /// <param name="username">The Telegram username (optional).</param>
    /// <param name="firstName">The user's first name (optional).</param>
    /// <param name="lastName">The user's last name (optional).</param>
    /// <returns>The existing or newly created user.</returns>
    Task<Users> GetOrCreateUserAsync(long telegramId, string? username, string? firstName, string? lastName);

    /// <summary>
    /// Processes a donation transaction and updates relevant records.
    /// </summary>
    /// <param name="userId">The identifier of the user making the donation.</param>
    /// <param name="amount">The donation amount.</param>
    /// <param name="currency">The currency of the donation.</param>
    /// <param name="donationId">The unique payment provider identifier for the donation.</param>
    /// <returns>True if the donation was processed successfully; otherwise, false.</returns>
    Task<bool> ProcessDonationAsync(long userId, decimal amount, string currency, string donationId);
}