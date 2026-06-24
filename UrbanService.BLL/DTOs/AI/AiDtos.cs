namespace UrbanService.BLL.DTOs.AI;

using System.Text.Json.Serialization;

public class AiAnalysisResponseDto
{
    public int AnalysisResultId { get; set; }

    public Guid FeedbackId { get; set; }

    public string? ModelName { get; set; }

    public int? DetectedCategoryId { get; set; }

    public string? DetectedCategoryName { get; set; }

    public string? Sentiment { get; set; }

    public string? UrgencyLevel { get; set; }

    public string? Summary { get; set; }

    public IReadOnlyCollection<string> Keywords { get; set; } = [];

    public decimal? ConfidenceScore { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AiChatRequest
{
    public int? ConversationId { get; set; }

    [JsonConverter(typeof(NullableGuidJsonConverter))]
    public Guid? FeedbackId { get; set; }

    public string Message { get; set; } = null!;
}

public class AiChatResponse
{
    public int ConversationId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

public class AiConversationDto
{
    public int ConversationId { get; set; }

    public Guid? FeedbackId { get; set; }

    public string? FeedbackTitle { get; set; }

    public string? Title { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public string? LastMessage { get; set; }

    public int MessageCount { get; set; }
}

public class AiMessageDto
{
    public int MessageId { get; set; }

    public int ConversationId { get; set; }

    public string SenderType { get; set; } = null!;

    public string MessageText { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

public class AiHealthResponse
{
    public bool IsAvailable { get; set; }

    public string Model { get; set; } = null!;

    public string? Error { get; set; }
}

public class AiFeedbackReviewQueueStatusResponse
{
    public int PendingSubmittedCount { get; set; }

    public int AiReviewedCount { get; set; }

    public int AnalysisResultCount { get; set; }

    public DateTime? OldestSubmittedAt { get; set; }

    public bool IsAiAvailable { get; set; }

    public string Model { get; set; } = null!;
}
