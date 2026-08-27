namespace UrbanService.DAL.Entities;

public partial class ManagerAreaAssignment
{
    public int ManagerAreaAssignmentId { get; set; }

    public Guid ManagerUserId { get; set; }

    public int AreaId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User ManagerUser { get; set; } = null!;

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual User? UpdatedByUser { get; set; }
}
