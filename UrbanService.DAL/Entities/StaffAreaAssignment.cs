using System;

namespace UrbanService.DAL.Entities;

public partial class StaffAreaAssignment
{
    public int StaffAreaAssignmentId { get; set; }

    public Guid UserId { get; set; }

    public int AreaId { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? AssignedByUser { get; set; }

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
