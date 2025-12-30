namespace Bot.Services;

public partial class AdminStateService
{
    /// <summary>
    /// Represents the steps in the admin goal creation workflow.
    /// </summary>
    public enum AdminGoalStep
    {
        /// <summary>
        /// No active goal creation.
        /// </summary>
        None,

        /// <summary>
        /// Waiting for goal title input.
        /// </summary>
        WaitingForTitle,

        /// <summary>
        /// Waiting for goal description input.
        /// </summary>
        WaitingForDescription,

        /// <summary>
        /// Waiting for target amount input.
        /// </summary>
        WaitingForAmount,
    }
}