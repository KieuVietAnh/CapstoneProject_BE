namespace UrbanService.BLL.DTOs.SLA;

public class FeedbackSlaDto
{
    public long FeedbackSlaId { get; set; }

    public Guid FeedbackId { get; set; }

    public string? FeedbackTitle { get; set; }

    public int SlaPolicyId { get; set; }

    public string? PolicyName { get; set; }

    public int AreaId { get; set; }

    public string? AreaName { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Priority { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime ResponseDueAt { get; set; }

    public DateTime ResolutionDueAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int TotalPausedMinutes { get; set; }

    public string Status { get; set; } = null!;

    public string ResponseStatus { get; set; } = null!;

    public string ResolutionStatus { get; set; } = null!;

    public bool IsResponseBreached { get; set; }

    public bool IsResolutionBreached { get; set; }

    public bool IsCurrent { get; set; }

    public Guid StartedByUserId { get; set; }

    public string? StartedByUserName { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public string? CompletedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Số phút còn lại tới hạn phản hồi.
    /// Giá trị âm nghĩa là đã quá hạn.
    /// </summary>
    public double? RemainingResponseMinutes { get; set; }

    /// <summary>
    /// Số phút còn lại tới hạn xử lý.
    /// Giá trị âm nghĩa là đã quá hạn.
    /// </summary>
    public double? RemainingResolutionMinutes { get; set; }

    public IReadOnlyCollection<SlaEventDto> Events { get; set; }
        = Array.Empty<SlaEventDto>();

    public IReadOnlyCollection<SlaPauseHistoryDto> PauseHistories { get; set; }
        = Array.Empty<SlaPauseHistoryDto>();
}