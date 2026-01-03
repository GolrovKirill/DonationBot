// <copyright file="IGoalService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Data.Models;

namespace Services;

/// <summary>
/// Service for handling goal-related operations including statistics and administrative functions.
/// </summary>
public interface IGoalService
{
    /// <summary>
    /// Checks if a user has administrator privileges.
    /// </summary>
    /// <param name="telegramId">The Telegram user identifier.</param>
    /// <returns>True if the user is an administrator; otherwise, false.</returns>
    Task<bool> IsUserAdminAsync(long telegramId);

    /// <summary>
    /// Retrieves the currently active donation goal.
    /// </summary>
    /// <returns>The active donation goal if found; otherwise, null.</returns>
    Task<DonationGoal?> GetActiveGoalAsync();

    /// <summary>
    /// Gets startup statistics for display in the welcome message.
    /// </summary>
    /// <returns>Formatted statistics string for the start command.</returns>
    Task<string> GetStartStats();

    /// <summary>
    /// Gets detailed statistics for the current goal.
    /// </summary>
    /// <returns>Formatted statistics string for the stats command.</returns>
    Task<string> GetGoalStatsAsync();

    /// <summary>
    /// Creates a new donation goal with the specified parameters.
    /// </summary>
    /// <param name="title">The goal title.</param>
    /// <param name="description">The goal description.</param>
    /// <param name="targetAmount">The target amount for the goal.</param>
    /// <returns>The created donation goal.</returns>
    Task<DonationGoal> CreateGoalAsync(string title, string description, decimal targetAmount);
}