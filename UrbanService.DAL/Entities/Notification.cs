using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class Notification
{
    public int NotificationId { get; set; }

    public Guid UserId { get; set; }

    public int? AlertId { get; set; }

    public Guid? IncidentId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Type { get; set; } = null!;

    public bool IsRead { get; set; }

    public string? TargetUrl { get; set; }

    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual AreaAlert? Alert { get; set; }

    public virtual Incident? Incident { get; set; }

    public virtual User User { get; set; } = null!;
}
