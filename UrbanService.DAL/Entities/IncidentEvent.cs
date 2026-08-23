using System;

namespace UrbanService.DAL.Entities;

public partial class IncidentEvent
{
    public long IncidentEventId { get; set; }

    public Guid IncidentId { get; set; }

    public Guid? FeedbackId { get; set; }

    public string EventType { get; set; } = null!;

    public Guid? ActorUserId { get; set; }

    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Incident Incident { get; set; } = null!;

    public virtual Feedback? Feedback { get; set; }

    public virtual User? ActorUser { get; set; }
}
