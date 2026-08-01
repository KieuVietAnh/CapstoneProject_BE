namespace UrbanService.BLL.DTOs.SLA;

public class SlaPolicyDto
{
    public int SlaPolicyId { get; set; }

    public string PolicyName { get; set; } = null!;

    public int? AreaId { get; set; }

    public string? AreaName { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Priority { get; set; } = null!;

    public int ResponseTimeMinutes { get; set; }

    public int ResolutionTimeMinutes { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public bool IsCurrentlyEffective { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string? CreatedByUserName { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public string? UpdatedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}