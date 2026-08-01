namespace UrbanService.BLL.DTOs.SLA;

public class SlaPolicyQueryParameters
{
    public string? Search { get; set; }

    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string? Priority { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>
    /// Chỉ lấy policy đang có hiệu lực tại thời điểm hiện tại.
    /// </summary>
    public bool? IsCurrentlyEffective { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}