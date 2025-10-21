namespace pgaActivityTools.Models.Webhook;

public record StravaWebhookEvent
{
    public string? Object_type { get; init; } // "activity" ou "athlete"
    public string? Aspect_type { get; init; } // "create", "update", "delete"
    public long Object_id { get; init; } // ID de l'activité
    public long Owner_id { get; init; } // ID de l'athlète
    public int Subscription_id { get; init; }
    public long Event_time { get; init; } // Unix timestamp
}

