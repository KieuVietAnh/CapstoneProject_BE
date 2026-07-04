using System;

namespace UrbanService.DAL.Entities;

public partial class CoordinatorCoverage
{
    public int CoverageId { get; set; }

    public int CoordinatorId { get; set; }

    public int AreaId { get; set; }

    public int CategoryId { get; set; }

    public bool IsPrimary { get; set; }

    public int PriorityOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual UrbanServiceCategory Category { get; set; } = null!;

    public virtual ServiceProviderCoordinator Coordinator { get; set; } = null!;
}
