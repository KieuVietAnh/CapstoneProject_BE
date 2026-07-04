using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class AreaAlert
{
    public int AlertId { get; set; }

    public int AreaId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public int? CategoryId { get; set; }

    public int? HotspotId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string AlertType { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? RadiusMeters { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual UrbanServiceCategory? Category { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual AreaHotspot? Hotspot { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
