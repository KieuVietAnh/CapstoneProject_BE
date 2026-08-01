namespace UrbanService.BLL.DTOs.SLA;

public class SlaPolicyUpdateRequest
{
    public string PolicyName { get; set; } = null!;

    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string Priority { get; set; } = null!;

    public int ResponseTimeMinutes { get; set; }

    public int ResolutionTimeMinutes { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }
}