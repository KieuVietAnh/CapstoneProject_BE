namespace UrbanService.DAL.Entities;

public partial class FeedbackDuplicateCandidate
{
    public Guid DuplicateCandidateId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid PotentialParentFeedbackId { get; set; }

    public string Status { get; set; } = null!;

    public decimal? ConfidenceScore { get; set; }

    public string? Reason { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual Feedback PotentialParentFeedback { get; set; } = null!;

    public virtual User? ReviewedByUser { get; set; }
}