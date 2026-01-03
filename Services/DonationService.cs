// <copyright file="DonationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Data;
using Data.Models;
using Microsoft.Extensions.Logging;

namespace Services;

/// <summary>
/// Provides services for managing user donations and user data operations.
/// Implements the <see cref="IDonationService"/> interface.
/// </summary>
public class DonationService : IDonationService
{
    private readonly IDapperRepository repository;
    private readonly ILogger<DonationService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DonationService"/> class.
    /// </summary>
    /// <param name="repository">Data repository for database operations.</param>
    /// <param name="logger">Logger instance for tracking operations.</param>
    public DonationService(IDapperRepository repository, ILogger<DonationService> logger)
    {
        this.repository = repository;
        this.logger = logger;
        this.logger.LogDebug("DonationService initialized");
    }

    /// <summary>
    /// Retrieves an existing user by Telegram ID or creates a new user if not found.
    /// </summary>
    /// <param name="telegramId">The Telegram user identifier.</param>
    /// <param name="username">The Telegram username (optional).</param>
    /// <param name="firstName">The user's first name (optional).</param>
    /// <param name="lastName">The user's last name (optional).</param>
    /// <returns>
    /// The existing or newly created <see cref="Users"/> object.
    /// </returns>
    /// <remarks>
    /// This method implements a "get or create" pattern for user management.
    /// </remarks>
    public async Task<Users> GetOrCreateUserAsync(long telegramId, string? username, string? firstName, string? lastName)
    {
        logger.LogDebug("Getting or creating user with Telegram ID: {TelegramId}", telegramId);

        try
        {
            var user = await repository.GetUserByTelegramIdAsync(telegramId);

            if (user != null)
            {
                logger.LogDebug("Found existing user {UserId} for Telegram ID: {TelegramId}", user.Id, telegramId);
                return user;
            }

            logger.LogInformation(
                "Creating new user for Telegram ID: {TelegramId}, username: {Username}", telegramId, username);

            var newUser = new Users
            {
                TelegramId = telegramId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                Admin = false,
            };

            var createdUser = await repository.CreateUserAsync(newUser);
            logger.LogInformation(
                "Created new user {UserId} for Telegram ID: {TelegramId}", createdUser.Id, telegramId);

            return createdUser;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting or creating user with Telegram ID: {TelegramId}", telegramId);
            throw;
        }
    }

    /// <summary>
    /// Processes a donation transaction and updates the relevant goal's progress.
    /// </summary>
    /// <param name="userTelegramId">The Telegram identifier of the user making the donation.</param>
    /// <param name="amount">The donation amount.</param>
    /// <param name="currency">The currency code of the donation.</param>
    /// <param name="donationId">The unique identifier provided by the payment provider.</param>
    /// <returns>
    /// <c>true</c> if the donation was processed successfully; otherwise, <c>false</c>.
    /// </returns>
    public async Task<bool> ProcessDonationAsync(long userTelegramId, decimal amount, string currency, string donationId)
    {
        logger.LogInformation(
            "Processing donation {DonationId} for user {UserTelegramId}, amount: {Amount} {Currency}", donationId, userTelegramId, amount, currency);

        try
        {
            var existingDonation = await repository.GetDonationAsync(donationId);
            if (existingDonation != null)
            {
                logger.LogWarning(
                    "Donation {DonationId} has already been processed on {ProcessedDate}", donationId, existingDonation.CreatedAt);
                return true;
            }

            var goal = await repository.GetActiveGoalAsync();
            if (goal == null)
            {
                logger.LogError("No active goal found for donation {DonationId}", donationId);
                return false;
            }

            logger.LogDebug("Found active goal {GoalId} for donation {DonationId}", goal.Id, donationId);

            var donation = new Donation
            {
                UserTelegramId = userTelegramId,
                GoalId = goal.Id,
                Amount = amount,
                Currency = currency,
                ProviderPaymentId = donationId,
                Status = "completed",
            };

            var createdDonation = await repository.CreateDonationAsync(donation);
            logger.LogDebug(
                "Created donation record {DonationRecordId} for payment {DonationId}", createdDonation.Id, donationId);

            await repository.UpdateGoalCurrentAmountAsync(goal.Id, amount);

            logger.LogInformation(
                "Successfully processed donation {DonationId} for user {UserTelegramId}. " +
                "Amount: {Amount} {Currency} added to goal {GoalId}",
                donationId,
                userTelegramId,
                amount,
                currency,
                goal.Id);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error processing donation {DonationId} for user {UserTelegramId}, amount: {Amount} {Currency}",
                donationId,
                userTelegramId,
                amount,
                currency);
            return false;
        }
    }
}