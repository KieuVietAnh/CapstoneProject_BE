namespace UrbanService.BLL.DTOs.SLA;

public class SlaPauseHistoryDto
{
    public long SlaPauseHistoryId { get; set; }

    public long FeedbackSlaId { get; set; }

    public string ReasonCode { get; set; } = null!;

    public string? ReasonNote { get; set; }

    public DateTime PausedAt { get; set; }

    public DateTime? ResumedAt { get; set; }

    public int? PausedMinutes { get; set; }

    public Guid PausedByUserId { get; set; }

    public string? PausedByUserName { get; set; }

    public Guid? ResumedByUserId { get; set; }

    public string? ResumedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}