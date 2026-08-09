using System.Text.Json.Serialization;

namespace UrbanService.BLL.DTOs;

public class MessengerConversationDto
{
    public long ConversationId { get; set; }
    public string PageId { get; set; } = null!;
    public string SenderPsid { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? LocationText { get; set; }
    public int? AreaId { get; set; }
    public string? AreaName { get; set; }
    public Guid? FeedbackId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MessengerWebhookPayload
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("entry")]
    public List<MessengerWebhookEntry> Entry { get; set; } = [];
}

public class MessengerWebhookEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("messaging")]
    public List<MessengerMessagingEvent> Messaging { get; set; } = [];
}

public class MessengerMessagingEvent
{
    [JsonPropertyName("sender")]
    public MessengerParticipant? Sender { get; set; }

    [JsonPropertyName("message")]
    public MessengerIncomingMessage? Message { get; set; }

    [JsonPropertyName("postback")]
    public MessengerPostback? Postback { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

public class MessengerParticipant
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class MessengerIncomingMessage
{
    [JsonPropertyName("mid")]
    public string? Mid { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("is_echo")]
    public bool IsEcho { get; set; }

    [JsonPropertyName("attachments")]
    public List<MessengerAttachment> Attachments { get; set; } = [];
}

public class MessengerAttachment
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class MessengerPostback
{
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}
