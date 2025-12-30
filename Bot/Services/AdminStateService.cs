using Microsoft.Extensions.Logging;

namespace Bot.Services;

/// <summary>
/// Manages administrative state for goal creation workflow.
/// </summary>
public partial class AdminStateService
{
    private readonly ILogger<AdminStateService> logger;
    private readonly Dictionary<long, AdminGoalCreationState> adminStates = [];

    /// <summary>
    /// Represents the state of an admin user during goal creation process.
    /// </summary>
    public class AdminGoalCreationState
    {
        /// <summary>
        /// Gets or sets the chat identifier where goal creation is taking place.
        /// </summary>
        public long ChatId { get; set; }

        /// <summary>
        /// Gets or sets the goal title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the goal description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the target amount for the goal.
        /// </summary>
        public decimal? TargetAmount { get; set; }

        /// <summary>
        /// Gets or sets the current step in the goal creation process.
        /// </summary>
        public AdminGoalStep CurrentStep { get; set; } = AdminGoalStep.None;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminStateService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    public AdminStateService(ILogger<AdminStateService> logger)
    {
        this.logger = logger;
        this.logger.LogDebug("AdminStateService initialized");
    }

    /// <summary>
    /// Starts the goal creation process for an admin user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="chatId">The chat identifier.</param>
    public virtual void StartGoalCreation(long userId, long chatId)
    {
        adminStates[userId] = new AdminGoalCreationState
        {
            ChatId = chatId,
            CurrentStep = AdminGoalStep.WaitingForTitle,
        };

        logger.LogInformation("Started goal creation for admin user {UserId} in chat {ChatId}", userId, chatId);
    }

    /// <summary>
    /// Retrieves the current state for an admin user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The admin goal creation state if found; otherwise, null.</returns>
    public virtual AdminGoalCreationState? GetState(long userId)
    {
        var stateExists = adminStates.TryGetValue(userId, out var state);

        if (!stateExists)
        {
            logger.LogDebug("No state found for user {UserId}", userId);
        }

        return state;
    }

    /// <summary>
    /// Sets the title for the goal being created and advances to the next step.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="title">The goal title.</param>
    public virtual void SetTitle(long userId, string title)
    {
        if (adminStates.TryGetValue(userId, out var state))
        {
            state.Title = title;
            state.CurrentStep = AdminGoalStep.WaitingForDescription;

            logger.LogDebug("Set title for user {UserId}: {Title}", userId, title);
        }
        else
        {
            logger.LogWarning("Attempted to set title for non-existent user state: {UserId}", userId);
        }
    }

    /// <summary>
    /// Sets the description for the goal being created and advances to the next step.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="description">The goal description.</param>
    public virtual void SetDescription(long userId, string description)
    {
        if (adminStates.TryGetValue(userId, out var state))
        {
            state.Description = description;
            state.CurrentStep = AdminGoalStep.WaitingForAmount;

            logger.LogDebug("Set description for user {UserId}", userId);
        }
        else
        {
            logger.LogWarning("Attempted to set description for non-existent user state: {UserId}", userId);
        }
    }

    /// <summary>
    /// Sets the target amount for the goal being created and completes the process.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="amount">The target amount.</param>
    public void SetAmount(long userId, decimal amount)
    {
        if (adminStates.TryGetValue(userId, out var state))
        {
            state.TargetAmount = amount;
            state.CurrentStep = AdminGoalStep.None;

            logger.LogDebug("Set amount for user {UserId}: {Amount}", userId, amount);
        }
        else
        {
            logger.LogWarning("Attempted to set amount for non-existent user state: {UserId}", userId);
        }
    }

    /// <summary>
    /// Cancels the goal creation process for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    public virtual void CancelGoalCreation(long userId)
    {
        var removed = adminStates.Remove(userId);

        if (removed)
        {
            logger.LogInformation("Canceled goal creation for user {UserId}", userId);
        }
        else
        {
            logger.LogDebug("Attempted to cancel non-existent goal creation for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Checks if a user is currently in the process of creating a goal.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>True if the user is creating a goal; otherwise, false.</returns>
    public virtual bool IsUserCreatingGoal(long userId)
    {
        var isCreating = adminStates.ContainsKey(userId) &&
               adminStates[userId].CurrentStep != AdminGoalStep.None;

        logger.LogDebug("User {UserId} goal creation status: {IsCreating}", userId, isCreating);

        return isCreating;
    }

    /// <summary>
    /// Gets the number of active admin states (for monitoring purposes).
    /// </summary>
    /// <returns>The count of active admin states.</returns>
    public int GetActiveStateCount()
    {
        var count = adminStates.Count;
        logger.LogTrace("Current active admin states: {Count}", count);
        return count;
    }
}