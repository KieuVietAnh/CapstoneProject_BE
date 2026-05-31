using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackResolution
{
    public int ResolutionId { get; set; }

    public Guid FeedbackId { get; set; }

    public int OperatorId { get; set; }

    public Guid ResolvedByUserId { get; set; }

    public string ResolutionSummary { get; set; } = null!;

    public string ActionTaken { get; set; } = null!;

    public string? ResultNote { get; set; }

    public DateTime ResolvedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual ICollection<FeedbackResolutionAttachment> FeedbackResolutionAttachments { get; set; } = new List<FeedbackResolutionAttachment>();

    public virtual ServiceOperator Operator { get; set; } = null!;

    public virtual User ResolvedByUser { get; set; } = null!;
}
