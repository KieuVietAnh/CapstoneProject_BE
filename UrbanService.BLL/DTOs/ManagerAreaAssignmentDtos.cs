namespace UrbanService.BLL.DTOs;

public class ManagerAreaAssignmentQueryParameters
{
    public Guid? ManagerUserId { get; set; }

    public int? AreaId { get; set; }

    public bool? IsActive { get; set; }
}

public class ManagerAreaAssignmentDto
{
    public int ManagerAreaAssignmentId { get; set; }

    public Guid ManagerUserId { get; set; }

    public string ManagerName { get; set; } = null!;

    public string ManagerEmail { get; set; } = null!;

    public bool ManagerIsActive { get; set; }

    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public string? WardCode { get; set; }

    public bool AreaIsActive { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string CreatedByUserName { get; set; } = null!;

    public Guid? UpdatedByUserId { get; set; }

    public string? UpdatedByUserName { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class ManagerAreaAssignmentCreateRequest
{
    public Guid ManagerUserId { get; set; }

    public int AreaId { get; set; }
}

public class ManagerAreaAssignmentUpdateRequest
{
    public int AreaId { get; set; }
}

public class ManagerAreaAssignmentActiveRequest
{
    public bool IsActive { get; set; }
}
