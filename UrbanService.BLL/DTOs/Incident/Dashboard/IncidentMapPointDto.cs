namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Một sự vụ đã có tọa độ, dùng để chấm điểm lên bản đồ.
///
/// Chỉ những sự vụ có đủ cả vĩ độ và kinh độ mới xuất hiện ở đây, nên số điểm
/// có thể nhỏ hơn tổng số sự vụ của khu vực.
/// </summary>
public class IncidentMapPointDto
{
    public Guid IncidentId { get; set; }

    public string Title { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string Status { get; set; } = null!;

    public string? Priority { get; set; }

    public string Severity { get; set; } = null!;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string LocationText { get; set; } = null!;

    /// <summary>
    /// Số phản ánh đang được liên kết vào sự vụ. Dùng để đánh trọng số điểm
    /// trên bản đồ: điểm càng nhiều phản ánh thì càng nhiều người cùng báo.
    /// </summary>
    public int ReportCount { get; set; }

    /// <summary>
    /// Sự vụ chưa kết thúc và chưa bị hủy. Dùng để tô màu điểm trên bản đồ.
    /// </summary>
    public bool IsOpen { get; set; }

    public DateTime CreatedAt { get; set; }
}
