namespace UrbanService.DAL.Entities;

public class MessengerFeedbackConversation
{
    public long ConversationId { get; set; }

    public string PageId { get; set; } = null!;

    public string SenderPsid { get; set; } = null!;

    public string State { get; set; } = null!;

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? LocationText { get; set; }

    public int? AreaId { get; set; }

    public Guid? FeedbackId { get; set; }

    public string? LastMessageId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual OperatingArea? Area { get; set; }

    public virtual Feedback? Feedback { get; set; }

    public virtual ICollection<MessengerFeedbackDraftAttachment> DraftAttachments { get; set; }
        = new List<MessengerFeedbackDraftAttachment>();

    public virtual ICollection<MessengerFeedbackSubmission> Submissions { get; set; }
        = new List<MessengerFeedbackSubmission>();
}
