using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackResolution
{
    public int ResolutionId { get; set; }

    public Guid FeedbackId { get; set; }

    public int? ProviderReportId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public string ResolutionSummary { get; set; } = null!;

    public string ActionTaken { get; set; } = null!;

    public string? ResultNote { get; set; }

    public DateTime ResolvedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual FeedbackProviderReport? ProviderReport { get; set; }

    public virtual User CreatedByStaffUser { get; set; } = null!;
}
