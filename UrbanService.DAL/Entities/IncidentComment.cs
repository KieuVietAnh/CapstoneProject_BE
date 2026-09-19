namespace UrbanService.DAL.Entities;

public sealed class IncidentComment
{
    public Guid IncidentCommentId { get; set; }

    public Guid IncidentId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = null!;

    public int? SourceFeedbackCommentId { get; set; }

    public Guid? SourceFeedbackId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Incident Incident { get; set; } = null!;

    public User User { get; set; } = null!;

    public FeedbackComment? SourceFeedbackComment { get; set; }

    public Feedback? SourceFeedback { get; set; }
}
