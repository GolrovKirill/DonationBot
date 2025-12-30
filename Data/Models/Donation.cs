namespace Data.Models;

public class Donation
{
    public int Id { get; set; }

    public long UserTelegramId { get; set; }

    public int GoalId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RUB";

    public string? ProviderPaymentId { get; set; }

    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
