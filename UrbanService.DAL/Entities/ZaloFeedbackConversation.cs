namespace UrbanService.DAL.Entities;

public class ZaloFeedbackConversation
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

    public Guid? FeedbackId { get; set; }

    public string? LastMessageId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual OperatingArea? Area { get; set; }

    public virtual Feedback? Feedback { get; set; }

    public virtual ICollection<ZaloFeedbackSubmission> Submissions { get; set; }
        = new List<ZaloFeedbackSubmission>();

    public virtual ICollection<ZaloFeedbackDraftAttachment> DraftAttachments { get; set; }
        = new List<ZaloFeedbackDraftAttachment>();
}
