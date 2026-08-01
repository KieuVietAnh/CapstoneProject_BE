namespace UrbanService.BLL.DTOs.SLA;

public class SlaPolicyCreateRequest
{
    public string PolicyName { get; set; } = null!;

    /// <summary>
    /// Null nghĩa là áp dụng cho tất cả khu vực.
    /// </summary>
    public int? AreaId { get; set; }

    /// <summary>
    /// Null nghĩa là áp dụng cho tất cả loại phản ánh.
    /// </summary>
    public int? CategoryId { get; set; }

    public string Priority { get; set; } = null!;

    public int ResponseTimeMinutes { get; set; }

    public int ResolutionTimeMinutes { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}