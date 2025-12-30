using Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Dapper;
using Configurations;

namespace Data;

public class DapperRepository : IDapperRepository
{
    private readonly string connectionString;
    private readonly ILogger<DapperRepository> logger;

    public DapperRepository(IOptions<DatabaseConfig> config, ILogger<DapperRepository> logger)
    {
        connectionString = config.Value.ConnectionString;
        this.logger = logger;
        this.logger.LogDebug("DapperRepository initialized");
    }

    public async Task<Users?> GetUserByTelegramIdAsync(long telegramId)
    {
        logger.LogDebug("Retrieving user by Telegram ID: {TelegramId}", telegramId);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var user = await connection.QueryFirstOrDefaultAsync<Users>(
                "SELECT * FROM users WHERE telegram_id = @TelegramId",
                new { TelegramId = telegramId });

            logger.LogDebug("User retrieval {(Result)} for Telegram ID: {TelegramId}",
                user != null ? "succeeded" : "failed - not found", telegramId);

            return user;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving user by Telegram ID: {TelegramId}", telegramId);
            throw;
        }
    }

    public async Task<Users> CreateUserAsync(Users user)
    {
        logger.LogDebug("Creating user with Telegram ID: {TelegramId}", user.TelegramId);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                INSERT INTO users (telegram_id, username, first_name, last_name)
                VALUES (@TelegramId, @Username, @FirstName, @LastName)
                RETURNING *";

            var createdUser = await connection.QueryFirstAsync<Users>(sql, user);
            logger.LogInformation("User created successfully with ID: {UserId} for Telegram ID: {TelegramId}",
                createdUser.Id, user.TelegramId);

            return createdUser;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating user with Telegram ID: {TelegramId}", user.TelegramId);
            throw;
        }
    }

    public async Task<DonationGoal?> GetActiveGoalAsync()
    {
        logger.LogDebug("Retrieving active donation goal");

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                SELECT 
                    id AS Id,
                    title AS Title,
                    description AS Description,
                    target_amount AS TargetAmount,
                    current_amount AS CurrentAmount,
                    is_active AS IsActive,
                    created_at AS CreatedAt
                FROM donation_goals 
                WHERE is_active = true 
                ORDER BY created_at DESC 
                LIMIT 1";

            var goal = await connection.QueryFirstOrDefaultAsync<DonationGoal>(sql);
            logger.LogDebug("Active goal retrieval {(Result)}",
                goal != null ? $"succeeded - Goal ID: {goal.Id}" : "failed - no active goal");

            return goal;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving active donation goal");
            throw;
        }
    }

    public async Task<int> GetCountDonationsForActiveGoals()
    {
        logger.LogDebug("Retrieving count of donations for active goals");

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                SELECT COUNT(*) 
                FROM donations d
                INNER JOIN donation_goals g ON d.goal_id = g.id
                WHERE g.is_active = true";

            var count = await connection.QueryFirstOrDefaultAsync<int>(sql);
            logger.LogDebug("Retrieved {DonationCount} donations for active goals", count);

            return count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving count of donations for active goals");
            throw;
        }
    }

    public async Task<int> GetCountUsersForActiveGoals()
    {
        logger.LogDebug("Retrieving count of unique users for active goals");

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                SELECT COUNT(DISTINCT d.user_telegram_id) 
                FROM donations d
                INNER JOIN donation_goals g ON d.goal_id = g.id
                WHERE g.is_active = true";

            var count = await connection.QueryFirstOrDefaultAsync<int>(sql);
            logger.LogDebug("Retrieved {UserCount} unique users for active goals", count);

            return count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving count of unique users for active goals");
            throw;
        }
    }

    public async Task<DonationGoal> CreateGoalAsync(DonationGoal goal)
    {
        logger.LogInformation("Creating new donation goal: {GoalTitle}", goal.Title);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                UPDATE donation_goals SET is_active = false WHERE is_active = true;

                INSERT INTO donation_goals (title, description, target_amount)
                VALUES (@Title, @Description, @TargetAmount)
                RETURNING *";

            var createdGoal = await connection.QueryFirstAsync<DonationGoal>(sql, goal);
            logger.LogInformation("Goal created successfully with ID: {GoalId}, Target: {TargetAmount}",
                createdGoal.Id, createdGoal.TargetAmount);

            return createdGoal;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating donation goal: {GoalTitle}", goal.Title);
            throw;
        }
    }

    public async Task UpdateGoalCurrentAmountAsync(int goalId, decimal amountToAdd)
    {
        logger.LogDebug("Updating goal {GoalId} current amount by {AmountToAdd}", goalId, amountToAdd);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                UPDATE donation_goals 
                SET current_amount = current_amount + @AmountToAdd
                WHERE id = @GoalId";

            var affectedRows = await connection.ExecuteAsync(sql, new { GoalId = goalId, AmountToAdd = amountToAdd });

            if (affectedRows == 0)
            {
                logger.LogWarning("No rows affected when updating goal {GoalId} amount", goalId);
            }
            else
            {
                logger.LogDebug("Successfully updated goal {GoalId} amount by {AmountToAdd}", goalId, amountToAdd);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating goal {GoalId} current amount by {AmountToAdd}", goalId, amountToAdd);
            throw;
        }
    }

    public async Task<Donation> CreateDonationAsync(Donation donation)
    {
        logger.LogDebug("Creating donation for user {UserTelegramId} with amount {Amount} {Currency}",
            donation.UserTelegramId, donation.Amount, donation.Currency);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                INSERT INTO donations (user_telegram_id, goal_id, amount, currency, provider_payment_id, status)
                VALUES (@UserTelegramId, @GoalId, @Amount, @Currency, @ProviderPaymentId, @Status)
                RETURNING
                    id AS Id,
                    user_telegram_id AS UserTelegramId,
                    goal_id AS GoalId,
                    amount AS Amount,
                    currency AS Currency,
                    status AS Status,
                    provider_payment_id AS ProviderPaymentId,
                    created_at AS CreatedAt";

            var createdDonation = await connection.QueryFirstAsync<Donation>(sql, donation);
            logger.LogInformation("Donation created successfully with ID: {DonationId} for user {UserTelegramId}",
                createdDonation.Id, donation.UserTelegramId);

            return createdDonation;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating donation for user {UserTelegramId}", donation.UserTelegramId);
            throw;
        }
    }

    public async Task<Donation?> GetDonationAsync(string donationId)
    {
        logger.LogDebug("Retrieving donation by ID: {DonationId}", donationId);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            var sql = @"
                SELECT 
                    id AS Id,
                    user_telegram_id AS UserTelegramId,
                    goal_id AS GoalId,
                    amount AS Amount,
                    currency AS Currency,
                    provider_payment_id AS ProviderPaymentId,
                    status AS Status,
                    created_at AS CreatedAt
                FROM donations 
                WHERE provider_payment_id = @DonationId";

            var donation = await connection.QueryFirstOrDefaultAsync<Donation>(sql, new { DonationId = donationId });
            logger.LogDebug("Donation retrieval {(Result)} for ID: {DonationId}",
                donation != null ? "succeeded" : "failed - not found", donationId);

            return donation;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving donation by ID: {DonationId}", donationId);
            throw;
        }
    }
}