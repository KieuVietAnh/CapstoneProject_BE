namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Bộ lọc đã thực sự được áp dụng cho kết quả thống kê.
///
/// Trả kèm tên đọc được của danh mục và phường để client hiển thị ngay, không
/// phải tra ngược từ id. Tên trả về null khi không lọc theo tiêu chí đó, hoặc
/// khi id được truyền vào không tồn tại.
/// </summary>
public class IncidentDistributionFilterDto
{
    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int? AreaId { get; set; }

    public string? AreaName { get; set; }

    /// <summary>
    /// Khoảng thời gian đã áp dụng: all, 7d, 1m, 6m hoặc 1y.
    /// </summary>
    public string Range { get; set; } = "all";

    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>
    /// Mốc đầu khoảng, quy về UTC. Null khi lấy toàn bộ thời gian.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Thời điểm chốt số liệu, quy về UTC.
    /// </summary>
    public DateTime ToUtc { get; set; }
}
