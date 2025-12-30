using Data;
using Data.Models;
using Microsoft.Extensions.Logging;

namespace Services;

public class DonationService : IDonationService
{
    private readonly IDapperRepository repository;
    private readonly ILogger<DonationService> logger;

    public DonationService(IDapperRepository repository, ILogger<DonationService> logger)
    {
        this.repository = repository;
        this.logger = logger;
        this.logger.LogDebug("DonationService initialized");
    }

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

            logger.LogInformation("Creating new user for Telegram ID: {TelegramId}, username: {Username}",
                telegramId, username);

            var newUser = new Users
            {
                TelegramId = telegramId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                Admin = false,
            };

            var createdUser = await repository.CreateUserAsync(newUser);
            logger.LogInformation("Created new user {UserId} for Telegram ID: {TelegramId}",
                createdUser.Id, telegramId);

            return createdUser;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting or creating user with Telegram ID: {TelegramId}", telegramId);
            throw;
        }
    }

    public async Task<bool> ProcessDonationAsync(long userTelegramId, decimal amount, string currency, string donationId)
    {
        logger.LogInformation("Processing donation {DonationId} for user {UserTelegramId}, amount: {Amount} {Currency}",
            donationId, userTelegramId, amount, currency);

        try
        {
            var existingDonation = await repository.GetDonationAsync(donationId);
            if (existingDonation != null)
            {
                logger.LogWarning("Donation {DonationId} has already been processed on {ProcessedDate}",
                    donationId, existingDonation.CreatedAt);
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
            logger.LogDebug("Created donation record {DonationRecordId} for payment {DonationId}",
                createdDonation.Id, donationId);

            await repository.UpdateGoalCurrentAmountAsync(goal.Id, amount);

            logger.LogInformation(
                "Successfully processed donation {DonationId} for user {UserTelegramId}. " +
                "Amount: {Amount} {Currency} added to goal {GoalId}",
                donationId, userTelegramId, amount, currency, goal.Id);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing donation {DonationId} for user {UserTelegramId}, amount: {Amount} {Currency}",
                donationId, userTelegramId, amount, currency);
            return false;
        }
    }
}