namespace UrbanService.DAL.Entities;

public class ZaloWebhookEvent
{
    public long WebhookEventId { get; set; }

    public string EventKey { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
