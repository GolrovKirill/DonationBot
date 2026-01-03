// <copyright file="DonationGoal.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Data.Models;

/// <summary>
/// Represents a fundraising goal with a target amount and current progress.
/// </summary>
public class DonationGoal
{
    /// <summary>
    /// Gets or sets the unique identifier for the donation goal.
    /// </summary>
    /// <value>
    /// The goal identifier.
    /// </value>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the title of the donation goal.
    /// </summary>
    /// <value>
    /// The goal title.
    /// </value>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional description providing more details about the goal.
    /// </summary>
    /// <value>
    /// The goal description, or null if not provided.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the target amount to be raised for this goal.
    /// </summary>
    /// <value>
    /// The target amount in the base currency.
    /// </value>
    public decimal TargetAmount { get; set; }

    /// <summary>
    /// Gets or sets the current amount raised towards the target.
    /// </summary>
    /// <value>
    /// The current amount raised.
    /// </value>
    public decimal CurrentAmount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the goal is currently active and accepting donations.
    /// </summary>
    /// <value>
    /// <c>true</c> if the goal is active; otherwise, <c>false</c>.
    /// </value>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the date and time when the goal was created.
    /// </summary>
    /// <value>
    /// The creation timestamp in UTC.
    /// </value>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
