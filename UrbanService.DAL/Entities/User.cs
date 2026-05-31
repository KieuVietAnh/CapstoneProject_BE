using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public int? OperatorId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; }

    public string? RefreshToken { get; set; }

    public bool IsRefreshTokenRevoked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AiConversation> AiConversations { get; set; } = new List<AiConversation>();

    public virtual ICollection<Feedback> FeedbackApprovedByManagers { get; set; } = new List<Feedback>();

    public virtual ICollection<FeedbackAssignment> FeedbackAssignments { get; set; } = new List<FeedbackAssignment>();

    public virtual ICollection<FeedbackComment> FeedbackComments { get; set; } = new List<FeedbackComment>();

    public virtual ICollection<FeedbackResolutionReview> FeedbackResolutionReviews { get; set; } = new List<FeedbackResolutionReview>();

    public virtual ICollection<FeedbackResolution> FeedbackResolutions { get; set; } = new List<FeedbackResolution>();

    public virtual ICollection<FeedbackStatusHistory> FeedbackStatusHistories { get; set; } = new List<FeedbackStatusHistory>();

    public virtual ICollection<FeedbackSupport> FeedbackSupports { get; set; } = new List<FeedbackSupport>();

    public virtual ICollection<Feedback> FeedbackUsers { get; set; } = new List<Feedback>();

    public virtual ICollection<InteractionMessage> InteractionMessages { get; set; } = new List<InteractionMessage>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ServiceOperator? Operator { get; set; }

    public virtual Role Role { get; set; } = null!;
}
