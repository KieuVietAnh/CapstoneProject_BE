namespace UrbanService.BLL.Dtos;

public class FeedbackDuplicateQueryParameters
{
    public string? Status { get; set; } = "Pending";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public class FeedbackDuplicateSummaryDto
{
    public int PendingCount { get; set; }

    public int ConfirmedCount { get; set; }

    public int RejectedCount { get; set; }

    public int TotalCount { get; set; }
}

public class FeedbackDuplicateCandidateDto
{
    public Guid DuplicateCandidateId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid PotentialParentFeedbackId { get; set; }

    public Guid? IncidentId { get; set; }

    /// <summary>Incident active hiện tại của Report đang được đánh giá.</summary>
    public Guid? CurrentIncidentId { get; set; }

    /// <summary>Incident active của Report canonical được đề xuất.</summary>
    public Guid? SuggestedIncidentId { get; set; }

    /// <summary>Hai Report hiện đã có cùng active Incident hay chưa.</summary>
    public bool AreInSameIncident { get; set; }

    public string Status { get; set; } = null!;

    public decimal? ConfidenceScore { get; set; }

    public string? Reason { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public string? ReviewedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public FeedbackListItemDto Feedback { get; set; } = null!;

    public FeedbackListItemDto PotentialParentFeedback { get; set; } = null!;
}

public class RelatedFeedbacksDto
{
    public Guid FeedbackId { get; set; }

    public Guid MasterFeedbackId { get; set; }

    public FeedbackListItemDto MasterFeedback { get; set; } = null!;

    public IReadOnlyCollection<FeedbackListItemDto> LinkedFeedbacks { get; set; } = [];
}
