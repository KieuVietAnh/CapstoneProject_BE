namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Số đếm gọn theo phường, không kèm tọa độ. Dùng cho các widget chỉ cần con số.
/// </summary>
public class IncidentAreaCountDto
{
    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public string? WardCode { get; set; }

    public string? DistrictName { get; set; }

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}
