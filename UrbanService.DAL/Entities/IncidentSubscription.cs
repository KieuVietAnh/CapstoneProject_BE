using System;

namespace UrbanService.DAL.Entities;

public partial class IncidentSubscription
{
    public Guid IncidentSubscriptionId { get; set; }

    public Guid IncidentId { get; set; }

    public Guid UserId { get; set; }

    public string SourceType { get; set; } = null!;

    public Guid? SourceFeedbackId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Incident Incident { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual Feedback? SourceFeedback { get; set; }
}
