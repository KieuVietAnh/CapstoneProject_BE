using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class UrbanServiceCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AiKnowledgeSource> AiKnowledgeSources { get; set; } = new List<AiKnowledgeSource>();

    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

    public virtual ICollection<AreaAlert> AreaAlerts { get; set; } = new List<AreaAlert>();

    public virtual ICollection<AreaHotspot> AreaHotspots { get; set; } = new List<AreaHotspot>();

    public virtual ICollection<CoordinatorCoverage> CoordinatorCoverages { get; set; } = new List<CoordinatorCoverage>();

    public virtual ICollection<SlaPolicy> SlaPolicies { get; set; }
    = new List<SlaPolicy>();

    public virtual ICollection<FeedbackSla> FeedbackSlas { get; set; }
        = new List<FeedbackSla>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<ProviderContract> ProviderContracts { get; set; } = new List<ProviderContract>();
}
