using System;

namespace UrbanService.DAL.Entities;

public partial class IncidentReportLink
{
    public Guid IncidentReportLinkId { get; set; }

    public Guid IncidentId { get; set; }

    public Guid FeedbackId { get; set; }

    public string LinkStatus { get; set; } = null!;

    public string LinkMethod { get; set; } = null!;

    public string LinkRole { get; set; } = null!;

    public decimal? ConfidenceScore { get; set; }

    public string? Reason { get; set; }

    public Guid? LinkedByUserId { get; set; }

    public DateTime LinkedAt { get; set; }

    public Guid? UnlinkedByUserId { get; set; }

    public DateTime? UnlinkedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Incident Incident { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual User? LinkedByUser { get; set; }

    public virtual User? UnlinkedByUser { get; set; }
}
