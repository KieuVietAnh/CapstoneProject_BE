using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class AreaHotspot
{
    public int HotspotId { get; set; }

    public int AreaId { get; set; }

    public int CategoryId { get; set; }

    public decimal? CenterLatitude { get; set; }

    public decimal? CenterLongitude { get; set; }

    public int? RadiusMeters { get; set; }

    public DateTime TimeWindowStart { get; set; }

    public DateTime TimeWindowEnd { get; set; }

    public int FeedbackCount { get; set; }

    public int MasterTicketCount { get; set; }

    public decimal? AveragePriorityScore { get; set; }

    public string RiskLevel { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string DetectedBy { get; set; } = null!;

    public string? SourceQueryJson { get; set; }

    public DateTime FirstDetectedAt { get; set; }

    public DateTime LastCalculatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual UrbanServiceCategory Category { get; set; } = null!;

    public virtual ICollection<AreaAlert> AreaAlerts { get; set; } = new List<AreaAlert>();
}
