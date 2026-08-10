using System.Text.Json.Serialization;

namespace UrbanService.BLL.DTOs;

public class ZaloConversationDto
{
    public long ConversationId { get; set; }
    public string OaId { get; set; } = null!;
    public string SenderUserId { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? LocationText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? AreaId { get; set; }
    public string? AreaName { get; set; }
    public Guid? FeedbackId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ZaloWebhookPayload
{
    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }

    [JsonPropertyName("sender")]
    public ZaloParticipant? Sender { get; set; }

    [JsonPropertyName("recipient")]
    public ZaloParticipant? Recipient { get; set; }

    [JsonPropertyName("event_name")]
    public string? EventName { get; set; }

    [JsonPropertyName("message")]
    public ZaloIncomingMessage? Message { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}

public class ZaloParticipant
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class ZaloIncomingMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("msg_id")]
    public string? MessageId { get; set; }

    [JsonPropertyName("attachments")]
    public List<ZaloAttachment> Attachments { get; set; } = [];
}

public class ZaloAttachment
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("payload")]
    public ZaloAttachmentPayload? Payload { get; set; }
}

public class ZaloAttachmentPayload
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("coordinates")]
    public ZaloCoordinates? Coordinates { get; set; }
}

public class ZaloCoordinates
{
    [JsonPropertyName("latitude")]
    public string? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public string? Longitude { get; set; }
}
