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

public class AiHealthResponse
{
    public bool IsAvailable { get; set; }

    public string Model { get; set; } = null!;

    public string? Error { get; set; }
}
