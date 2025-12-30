using Data;
using Data.Models;
using Microsoft.Extensions.Logging;

namespace Services;

/// <summary>
/// Service implementation for handling goal-related operations including statistics and administrative functions.
/// </summary>
public class GoalService : IGoalService
{
    private readonly IDapperRepository repository;
    private readonly ILogger<GoalService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoalService"/> class.
    /// </summary>
    /// <param name="repository">The repository for data access operations.</param>
    /// <param name="logger">Logger instance for tracking operations.</param>
    public GoalService(IDapperRepository repository, ILogger<GoalService> logger)
    {
        this.repository = repository;
        this.logger = logger;

        this.logger.LogDebug("GoalService initialized");
    }

    /// <inheritdoc/>
    public async Task<bool> IsUserAdminAsync(long telegramId)
    {
        logger.LogDebug("Checking admin status for user {TelegramId}", telegramId);

        try
        {
            var user = await repository.GetUserByTelegramIdAsync(telegramId);
            var isAdmin = user?.Admin ?? false;

            logger.LogDebug("User {TelegramId} admin status: {IsAdmin}", telegramId, isAdmin);

            return isAdmin;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking admin status for user {TelegramId}", telegramId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<DonationGoal?> GetActiveGoalAsync()
    {
        logger.LogDebug("Retrieving active goal");

        try
        {
            var goal = await repository.GetActiveGoalAsync();

            logger.LogDebug("Active goal retrieval {(Result)}", goal != null ? $"succeeded - Goal ID: {goal.Id}" : "failed - no active goal");

            return goal;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving active goal");
            return null;
        }
    }

    private async Task<int> GetActiveUsersGoalAsync()
    {
        logger.LogDebug("Retrieving active users count for goal");

        try
        {
            var count = await repository.GetCountUsersForActiveGoals();

            logger.LogDebug("Retrieved {UserCount} active users for goal", count);

            return count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving active users count for goal");
            return 0;
        }
    }

    private async Task<int> GetActiveDonationsGoalAsync()
    {
        logger.LogDebug("Retrieving active donations count for goal");

        try
        {
            var count = await repository.GetCountDonationsForActiveGoals();

            logger.LogDebug("Retrieved {DonationCount} active donations for goal", count);

            return count;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving active donations count for goal");
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetGoalStatsAsync()
    {
        logger.LogDebug("Generating goal statistics");

        try
        {
            var goal = await GetActiveGoalAsync();
            var countUsers = await GetActiveUsersGoalAsync();
            var countDonations = await GetActiveDonationsGoalAsync();

            if (goal == null)
            {
                logger.LogInformation("No active goal found for statistics");
                return "🎯 На данный момент нет активных целей для сбора.";
            }

            var percent = goal.TargetAmount > 0
                ? ((double)goal.CurrentAmount / (double)goal.TargetAmount) * 100
                : 0;

            var progressBar = CreateProgressBar(percent);

            var stats = $"🎯 **{goal.Title}** — {goal.TargetAmount:N0}₽ \n📝 Описание: {goal.Description} \n\n📈 Количество пожертвований на текущую цель: {countDonations}" +
                $"\n🧮 Количество пожертвовавших: {countUsers} \n⏳ Дата открытия сбора: {goal.CreatedAt:dd.MM.yyyy}\n\nСобрано: {goal.CurrentAmount:N0}₽ ({percent:F1}%) \n{progressBar}";

            logger.LogDebug("Goal statistics generated successfully for goal {GoalId}", goal.Id);

            return stats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating goal statistics");
            return "❌ Произошла ошибка при получении статистики.";
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetStartStats()
    {
        logger.LogDebug("Generating start statistics");

        try
        {
            var goal = await GetActiveGoalAsync();

            if (goal == null)
            {
                logger.LogInformation("No active goal found for start statistics");
                return "🎯 На данный момент нет активных целей для сбора.";
            }

            var percent = goal.TargetAmount > 0
                ? ((double)goal.CurrentAmount / (double)goal.TargetAmount) * 100
                : 0;

            var progressBar = CreateProgressBar(percent);

            var stats = $"🎯 **{goal.Title}** — {goal.TargetAmount:N0}₽ \n📝 Описание: {goal.Description} \n\nСобрано: {goal.CurrentAmount:N0}₽ ({percent:F1}%) \n{progressBar}";

            logger.LogDebug("Start statistics generated successfully for goal {GoalId}", goal.Id);

            return stats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating start statistics");
            return "❌ Произошла ошибка при получении статистики.";
        }
    }

    /// <summary>
    /// Creates a visual progress bar representation.
    /// </summary>
    /// <param name="percent">The completion percentage.</param>
    /// <returns>A string representing the progress bar.</returns>
    private string CreateProgressBar(double percent)
    {
        if (percent >= 100)
        {
            return $"[{new string('■', 10)}]";
        }
        else
        {
            var filled = (int)Math.Round(percent / 10);
            var empty = 10 - filled;
            return $"[{new string('■', filled)}{new string('□', empty)}]";
        }
    }

    /// <inheritdoc/>
    public async Task<DonationGoal> CreateGoalAsync(string title, string description, decimal targetAmount)
    {
        logger.LogInformation("Creating new goal: {Title}, Target: {TargetAmount}", title, targetAmount);

        try
        {
            var newGoal = new DonationGoal
            {
                Title = title,
                Description = description,
                TargetAmount = targetAmount,
                IsActive = true,
            };

            var createdGoal = await repository.CreateGoalAsync(newGoal);

            logger.LogInformation("Goal created successfully: {GoalId}, {Title}, Target: {TargetAmount}", createdGoal.Id, title, targetAmount);

            return createdGoal;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating goal: {Title}", title);
            throw;
        }
    }
}