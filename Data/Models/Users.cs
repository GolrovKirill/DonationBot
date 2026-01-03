// <copyright file="Users.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Data.Models;

/// <summary>
/// Represents a user in the system.
/// </summary>
public class Users
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has administrative privileges.
    /// </summary>
    public bool Admin { get; set; }

    /// <summary>
    /// Gets or sets the user's Telegram identifier.
    /// </summary>
    public long TelegramId { get; set; }

    /// <summary>
    /// Gets or sets the user's Telegram username (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the user's first name (optional).
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the user's last name (optional).
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the user record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}