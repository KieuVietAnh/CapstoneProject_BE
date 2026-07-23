using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; }

    public bool IsVerified { get; set; }

    public string? RefreshToken { get; set; }

    public bool IsRefreshTokenRevoked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<AiConversation> AiConversations { get; set; } = new List<AiConversation>();

    public virtual ICollection<AreaAlert> AreaAlerts { get; set; } = new List<AreaAlert>();

    public virtual ICollection<CompletionDocument> CompletionDocuments { get; set; } = new List<CompletionDocument>();

    public virtual ICollection<Feedback> FeedbackApprovedByManagers { get; set; } = new List<Feedback>();

    public virtual ICollection<FeedbackComment> FeedbackComments { get; set; } = new List<FeedbackComment>();

    public virtual ICollection<FeedbackDuplicateCandidate> FeedbackDuplicateCandidateReviews { get; set; } = new List<FeedbackDuplicateCandidate>();

    public virtual ICollection<FeedbackProviderReport> FeedbackProviderReports { get; set; } = new List<FeedbackProviderReport>();

    public virtual ICollection<FeedbackResolutionReview> FeedbackResolutionReviews { get; set; } = new List<FeedbackResolutionReview>();

    public virtual ICollection<FeedbackResolution> FeedbackResolutions { get; set; } = new List<FeedbackResolution>();

    public virtual ICollection<FeedbackStatusHistory> FeedbackStatusHistories { get; set; } = new List<FeedbackStatusHistory>();

    public virtual ICollection<FeedbackSupport> FeedbackSupports { get; set; } = new List<FeedbackSupport>();

    public virtual ICollection<Feedback> FeedbackUsers { get; set; } = new List<Feedback>();

    public virtual ICollection<InteractionMessage> InteractionMessages { get; set; } = new List<InteractionMessage>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<ProviderContactLog> ProviderContactLogs { get; set; } = new List<ProviderContactLog>();

    public virtual ICollection<ProviderContract> ProviderContracts { get; set; } = new List<ProviderContract>();

    public virtual ICollection<ProviderContractAttachment> ProviderContractAttachments { get; set; } = new List<ProviderContractAttachment>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<StaffAreaAssignment> StaffAreaAssignmentAssignedByUsers { get; set; } = new List<StaffAreaAssignment>();

    public virtual ICollection<StaffAreaAssignment> StaffAreaAssignmentUsers { get; set; } = new List<StaffAreaAssignment>();

    public virtual ICollection<UserAreaSubscription> UserAreaSubscriptions { get; set; } = new List<UserAreaSubscription>();
}