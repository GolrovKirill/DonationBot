using Data.Models;

namespace Data;

/// <summary>
/// Repository interface for data access operations using Dapper ORM.
/// </summary>
public interface IDapperRepository
{
    /// <summary>
    /// Retrieves a user by their Telegram identifier.
    /// </summary>
    /// <param name="telegramId">The Telegram user identifier.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    Task<Users?> GetUserByTelegramIdAsync(long telegramId);

    /// <summary>
    /// Creates a new user in the database.
    /// </summary>
    /// <param name="user">The user entity to create.</param>
    /// <returns>The created user with generated identifier.</returns>
    Task<Users> CreateUserAsync(Users user);

    /// <summary>
    /// Retrieves the currently active donation goal.
    /// </summary>
    /// <returns>The active donation goal if found; otherwise, null.</returns>
    Task<DonationGoal?> GetActiveGoalAsync();

    /// <summary>
    /// Gets the count of unique users who donated to active goals.
    /// </summary>
    /// <returns>The number of unique donors for active goals.</returns>
    Task<int> GetCountUsersForActiveGoals();

    /// <summary>
    /// Gets the total count of donations made to active goals.
    /// </summary>
    /// <returns>The total number of donations for active goals.</returns>
    Task<int> GetCountDonationsForActiveGoals();

    /// <summary>
    /// Creates a new donation goal in the database.
    /// </summary>
    /// <param name="goal">The donation goal entity to create.</param>
    /// <returns>The created donation goal with generated identifier.</returns>
    Task<DonationGoal> CreateGoalAsync(DonationGoal goal);

    /// <summary>
    /// Updates the current collected amount for a specific goal.
    /// </summary>
    /// <param name="goalId">The identifier of the goal to update.</param>
    /// <param name="newAmount">The new current amount value.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateGoalCurrentAmountAsync(int goalId, decimal newAmount);

    /// <summary>
    /// Creates a new donation record in the database.
    /// </summary>
    /// <param name="donation">The donation entity to create.</param>
    /// <returns>The created donation with generated identifier.</returns>
    Task<Donation> CreateDonationAsync(Donation donation);

    /// <summary>
    /// Retrieves a donation by its unique identifier.
    /// </summary>
    /// <param name="donationId">The donation identifier.</param>
    /// <returns>The donation if found; otherwise, null.</returns>
    Task<Donation?> GetDonationAsync(string donationId);
}