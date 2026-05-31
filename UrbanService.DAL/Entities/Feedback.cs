using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class Feedback
{
    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public int CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string LocationText { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string Priority { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? DueDate { get; set; }

    public Guid? ApprovedByManagerId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public bool IsMasterTicket { get; set; }

    public Guid? ParentTicketId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AiConversation> AiConversations { get; set; } = new List<AiConversation>();

    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

    public virtual User? ApprovedByManager { get; set; }

    public virtual UrbanServiceCategory Category { get; set; } = null!;

    public virtual ICollection<Channel> Channels { get; set; } = new List<Channel>();

    public virtual ICollection<FeedbackAssignment> FeedbackAssignments { get; set; } = new List<FeedbackAssignment>();

    public virtual ICollection<FeedbackAttachment> FeedbackAttachments { get; set; } = new List<FeedbackAttachment>();

    public virtual ICollection<FeedbackComment> FeedbackComments { get; set; } = new List<FeedbackComment>();

    public virtual ICollection<FeedbackResolutionReview> FeedbackResolutionReviews { get; set; } = new List<FeedbackResolutionReview>();

    public virtual ICollection<FeedbackResolution> FeedbackResolutions { get; set; } = new List<FeedbackResolution>();

    public virtual ICollection<FeedbackStatusHistory> FeedbackStatusHistories { get; set; } = new List<FeedbackStatusHistory>();

    public virtual ICollection<FeedbackSupport> FeedbackSupports { get; set; } = new List<FeedbackSupport>();

    public virtual ICollection<InteractionMessage> InteractionMessages { get; set; } = new List<InteractionMessage>();

    public virtual ICollection<Feedback> InverseParentTicket { get; set; } = new List<Feedback>();

    public virtual Feedback? ParentTicket { get; set; }

    public virtual User User { get; set; } = null!;
}
