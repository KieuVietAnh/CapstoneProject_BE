using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class OperatingArea
{
    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public string AreaType { get; set; } = null!;

    public string? WardCode { get; set; }

    public string? DistrictName { get; set; }

    public string? ProvinceName { get; set; }

    public decimal? CenterLatitude { get; set; }

    public decimal? CenterLongitude { get; set; }

    public string? BoundaryGeoJson { get; set; }

    public bool IsActive { get; set; }

    public DateOnly? StartedAt { get; set; }

    public DateOnly? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AiKnowledgeSource> AiKnowledgeSources { get; set; } = new List<AiKnowledgeSource>();

    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

    public virtual ICollection<AreaAlert> AreaAlerts { get; set; } = new List<AreaAlert>();

    public virtual ICollection<AreaHotspot> AreaHotspots { get; set; } = new List<AreaHotspot>();

    public virtual ICollection<CoordinatorCoverage> CoordinatorCoverages { get; set; } = new List<CoordinatorCoverage>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<ManagerAreaAssignment> ManagerAreaAssignments { get; set; } = new List<ManagerAreaAssignment>();

    public virtual ICollection<MessengerFeedbackConversation> MessengerFeedbackConversations { get; set; }
        = new List<MessengerFeedbackConversation>();

    public virtual ICollection<ZaloFeedbackConversation> ZaloFeedbackConversations { get; set; }
        = new List<ZaloFeedbackConversation>();

    public virtual ICollection<ProviderContract> ProviderContracts { get; set; } = new List<ProviderContract>();

    public virtual ICollection<SlaPolicy> SlaPolicies { get; set; }
    = new List<SlaPolicy>();

    public virtual ICollection<FeedbackSla> FeedbackSlas { get; set; }
        = new List<FeedbackSla>();

    public virtual ICollection<StaffAreaAssignment> StaffAreaAssignments { get; set; } = new List<StaffAreaAssignment>();

    public virtual ICollection<UserAreaSubscription> UserAreaSubscriptions { get; set; } = new List<UserAreaSubscription>();
}
