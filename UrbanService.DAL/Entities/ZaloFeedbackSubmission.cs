namespace UrbanService.DAL.Entities;

public class ZaloFeedbackSubmission
{
    public long SubmissionId { get; set; }

    public long ConversationId { get; set; }

    public Guid FeedbackId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ZaloFeedbackConversation Conversation { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;
}
