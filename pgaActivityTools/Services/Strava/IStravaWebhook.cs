using pgaActivityTools.Models.Webhook;

namespace pgaActivityTools.Services.Strava;

public interface IStravaWebhook
{
    Task<WebhookSubscription?> CreateSubscriptionAsync();

    Task<List<WebhookSubscription>?> GetSubscriptionsAsync();
    Task<bool> DeleteSubscriptionAsync(int subscriptionId);
    Task ProcessActivityEventAsync(StravaWebhookEvent webhookEvent);
}
