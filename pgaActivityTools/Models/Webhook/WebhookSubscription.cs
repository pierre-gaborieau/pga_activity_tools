namespace pgaActivityTools.Models.Webhook;

public record WebhookSubscription
{
    public int Id { get; init; }
    public string? Callback_url { get; init; }
}
