namespace UrbanService.BLL.Dtos;

public sealed class IncidentQueryParameters
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string? Status { get; set; }

    public string? Priority { get; set; }

    public string? Severity { get; set; }

    public string? Search { get; set; }

    public bool IncludeMerged { get; set; }

    public Guid? AssignedStaffUserId { get; set; }
}

public class IncidentListItemDto
{
    public Guid IncidentId { get; set; }

    public int AreaId { get; set; }

    public string? AreaName { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Title { get; set; } = null!;

    public string LocationText { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Priority { get; set; }

    public string Severity { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid? MergedIntoIncidentId { get; set; }

    public Guid? AssignedStaffUserId { get; set; }

    public string? AssignedStaffName { get; set; }

    public int ReportCount { get; set; }

    public int SubscriberCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class IncidentDetailDto : IncidentListItemDto
{
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public IReadOnlyCollection<IncidentReportDto> Reports { get; set; } = [];

    public IReadOnlyCollection<IncidentSubscriberDto> Subscribers { get; set; } = [];

    public IReadOnlyCollection<IncidentEventDto> Events { get; set; } = [];
}

public sealed class IncidentReportDto
{
    public Guid IncidentReportLinkId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid ReporterUserId { get; set; }

    public string? ReporterName { get; set; }

    public string Title { get; set; } = null!;

    public string LocationText { get; set; } = null!;

    public string SubmissionChannel { get; set; } = null!;

    public string FeedbackStatus { get; set; } = null!;

    public string LinkStatus { get; set; } = null!;

    public string LinkMethod { get; set; } = null!;

    public string LinkRole { get; set; } = null!;

    public decimal? ConfidenceScore { get; set; }

    public string? Reason { get; set; }

    public Guid? LinkedByUserId { get; set; }

    public string? LinkedByUserName { get; set; }

    public DateTime LinkedAt { get; set; }

    public Guid? UnlinkedByUserId { get; set; }

    public string? UnlinkedByUserName { get; set; }

    public DateTime? UnlinkedAt { get; set; }
}

public sealed class IncidentSubscriberDto
{
    public Guid UserId { get; set; }

    public string? UserName { get; set; }

    public string SourceType { get; set; } = null!;

    public Guid? SourceFeedbackId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class IncidentEventDto
{
    public long IncidentEventId { get; set; }

    public Guid? FeedbackId { get; set; }

    public string EventType { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    public string? ActorUserName { get; set; }

    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class LinkIncidentReportRequest
{
    public Guid FeedbackId { get; set; }

    public string LinkMethod { get; set; } = "StaffConfirmed";

    public decimal? ConfidenceScore { get; set; }

    public string? Reason { get; set; }
}

public sealed class UpdateIncidentRequest
{
    public int? AreaId { get; set; }
    public int? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? LocationText { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Priority { get; set; }
    public string? Severity { get; set; }
    public DateTime? DueDate { get; set; }
}

public sealed class UpdateIncidentStatusRequest
{
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
}

public sealed class AssignIncidentRequest
{
    public Guid StaffUserId { get; set; }
    public string? Reason { get; set; }
}

public sealed class MergeIncidentRequest
{
    public Guid TargetIncidentId { get; set; }
    public string? Reason { get; set; }
}

public sealed class IncidentAssigneeCandidateDto
{
    public Guid UserId { get; set; }
    public string StaffName { get; set; } = null!;
    public string? Email { get; set; }
    public int AreaId { get; set; }
    public string? AreaName { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public bool IsPrimary { get; set; }
}

public class PublicIncidentListItemDto
{
    public Guid IncidentId { get; set; }
    public int AreaId { get; set; }
    public string? AreaName { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string LocationText { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Priority { get; set; }
    public string Severity { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int ReportCount { get; set; }
    public int SubscriberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class PublicIncidentDetailDto : PublicIncidentListItemDto
{
    public DateTime? DueDate { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsSubscribedByCurrentUser { get; set; }
}

public sealed class PublicIncidentReportDto
{
    public Guid FeedbackId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string LocationText { get; set; } = null!;
    public string SubmissionChannel { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public IReadOnlyCollection<FeedbackAttachmentDto> Attachments { get; set; } = [];
}

public sealed class PublicIncidentEventDto
{
    public long IncidentEventId { get; set; }
    public string EventType { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
