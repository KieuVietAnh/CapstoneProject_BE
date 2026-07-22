using UrbanService.BLL.DTOs.AI;

namespace UrbanService.BLL.Dtos;

public class FeedbackQueryParameters
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? Status { get; set; }

    public int? CategoryId { get; set; }

    public string? Search { get; set; }
}

public class FeedbackCreateRequest
{
    public int AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string LocationText { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? LocationAccuracyMeters { get; set; }

    public string? GeoSource { get; set; }

    public string? Priority { get; set; }

    public DateTime? DueDate { get; set; }
}

public class FeedbackUpdateRequest
{
    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? LocationText { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? LocationAccuracyMeters { get; set; }

    public string? GeoSource { get; set; }

    public string? Priority { get; set; }

    public DateTime? DueDate { get; set; }
}

public class StaffFeedbackUpdateRequest : FeedbackUpdateRequest
{
    /// <summary>
    /// Trạng thái mới. Giá trị hợp lệ: Submitted, AiReviewed, Verified, Assigned, InProgress,
    /// Resolved, SubmittedForApproval, Approved, Rejected, NeedRework, Closed, Cancelled.
    /// </summary>
    /// <example>InProgress</example>
    public string? Status { get; set; }

    /// <summary>Ghi chú cho lần thay đổi trạng thái.</summary>
    public string? StatusNote { get; set; }
}

public class UpdateFeedbackStatusRequest
{
    /// <summary>
    /// Trạng thái mới. Giá trị hợp lệ: Submitted, AiReviewed, Verified, Assigned, InProgress,
    /// Resolved, SubmittedForApproval, Approved, Rejected, NeedRework, Closed, Cancelled.
    /// </summary>
    /// <example>InProgress</example>
    public string Status { get; set; } = null!;

    /// <summary>Ghi chú cho lần thay đổi trạng thái.</summary>
    public string? Note { get; set; }
}

public class FeedbackCommentCreateRequest
{
    public string Content { get; set; } = null!;
}

public class UploadedFeedbackAttachmentDto
{
    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }
}

public class FeedbackListItemDto
{
    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public string? UserName { get; set; }

    public int AreaId { get; set; }

    public string? AreaName { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Title { get; set; } = null!;

    public string LocationText { get; set; } = null!;

    public string? Priority { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int AttachmentCount { get; set; }

    public int CommentCount { get; set; }

    public int SupportCount { get; set; }
}

public class FeedbackDetailDto : FeedbackListItemDto
{
    public string Description { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? LocationAccuracyMeters { get; set; }

    public string? GeoSource { get; set; }

    public bool IsLocationVerified { get; set; }

    public DateTime? DueDate { get; set; }

    public bool IsSupportedByCurrentUser { get; set; }

    public IReadOnlyCollection<FeedbackAttachmentDto> Attachments { get; set; } = [];

    public IReadOnlyCollection<FeedbackCommentDto> Comments { get; set; } = [];

    public IReadOnlyCollection<FeedbackStatusHistoryDto> StatusHistories { get; set; } = [];
}

public class FeedbackWithAnalysisResultDto
{
    public FeedbackListItemDto Feedback { get; set; } = null!;

    public AiAnalysisResponseDto? AnalysisResult { get; set; }
}

public class FeedbackAttachmentDto
{
    public int AttachmentId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public DateTime UploadedAt { get; set; }
}

public class FeedbackCommentDto
{
    public int CommentId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public string? UserName { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

public class FeedbackStatusHistoryDto
{
    public int HistoryId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid ChangedByUserId { get; set; }

    public string? ChangedByUserName { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }
}
