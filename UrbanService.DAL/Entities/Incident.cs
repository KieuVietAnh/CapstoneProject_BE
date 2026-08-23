using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class Incident
{
    public Guid IncidentId { get; set; }

    public int AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string LocationText { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Priority { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? DueDate { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid? MergedIntoIncidentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual UrbanServiceCategory? Category { get; set; }

    public virtual Incident? MergedIntoIncident { get; set; }

    public virtual ICollection<Incident> MergedIncidents { get; set; } = new List<Incident>();

    public virtual ICollection<IncidentEvent> IncidentEvents { get; set; } = new List<IncidentEvent>();

    public virtual ICollection<IncidentReportLink> IncidentReportLinks { get; set; } = new List<IncidentReportLink>();

    public virtual ICollection<IncidentSubscription> IncidentSubscriptions { get; set; } = new List<IncidentSubscription>();
}
