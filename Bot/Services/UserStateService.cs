using Microsoft.Extensions.Logging;

namespace Bot.Services;

/// <summary>
/// Manages user state for tracking user interactions and input expectations.
/// </summary>
public class UserStateService
{
    private readonly ILogger<UserStateService> logger;
    private readonly Dictionary<long, long> waitingForAmount = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="UserStateService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for tracking operations.</param>
    public UserStateService(ILogger<UserStateService> logger)
    {
        this.logger = logger;
        this.logger.LogDebug("UserStateService initialized");
    }

    /// <summary>
    /// Sets a user to waiting for amount input state.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="chatId">The chat identifier.</param>
    public virtual void SetWaitingForAmount(long userId, long chatId)
    {
        waitingForAmount[userId] = chatId;
        logger.LogDebug("User {UserId} set to waiting for amount input in chat {ChatId}", userId, chatId);
    }

    /// <summary>
    /// Checks if a user is waiting for amount input in the specified chat.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="chatId">The chat identifier.</param>
    /// <returns>True if the user is waiting for amount input in the specified chat; otherwise, false.</returns>
    public virtual bool IsWaitingForAmount(long userId, long chatId)
    {
        var isWaiting = waitingForAmount.TryGetValue(userId, out var waitingChatId) && waitingChatId == chatId;

        logger.LogDebug(
            "Checked waiting for amount status for user {UserId} in chat {ChatId}: {IsWaiting}", userId, chatId, isWaiting);

        return isWaiting;
    }

    /// <summary>
    /// Removes a user from waiting for amount input state.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    public virtual void RemoveWaitingForAmount(long userId)
    {
        var removed = waitingForAmount.Remove(userId);

        if (removed)
        {
            logger.LogDebug("Removed user {UserId} from waiting for amount state", userId);
        }
        else
        {
            logger.LogDebug("Attempted to remove non-existent waiting state for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Gets the number of users currently waiting for amount input.
    /// </summary>
    /// <returns>The count of users in waiting for amount state.</returns>
    public int GetWaitingUsersCount()
    {
        var count = waitingForAmount.Count;
        logger.LogTrace("Current users waiting for amount input: {Count}", count);
        return count;
    }

    /// <summary>
    /// Clears all waiting states (for maintenance or reset scenarios).
    /// </summary>
    public void ClearAllWaitingStates()
    {
        var count = waitingForAmount.Count;
        waitingForAmount.Clear();
        logger.LogInformation("Cleared all waiting states, affected {Count} users", count);
    }

    /// <summary>
    /// Removes waiting state for multiple users at once.
    /// </summary>
    /// <param name="userIds">The collection of user identifiers to remove.</param>
    /// <returns>The number of users successfully removed.</returns>
    public int RemoveMultipleWaitingStates(IEnumerable<long> userIds)
    {
        var removedCount = 0;

        foreach (var userId in userIds)
        {
            if (waitingForAmount.Remove(userId))
            {
                removedCount++;
            }
        }

        logger.LogDebug("Removed waiting states for {RemovedCount} out of {TotalCount} users", removedCount, userIds.Count());

        return removedCount;
    }
}