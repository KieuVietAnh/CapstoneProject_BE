namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Tiêu chí lọc cho thống kê sự vụ theo danh mục và phường.
///
/// Mọi tiêu chí đều tùy chọn. Bỏ trống hết thì lấy toàn bộ sự vụ trong phạm vi
/// đọc của người dùng.
/// </summary>
public class IncidentDistributionQueryParameters
{
    /// <summary>
    /// Chỉ lấy sự vụ thuộc danh mục này. Bỏ trống để lấy mọi danh mục.
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Chỉ lấy sự vụ thuộc phường này. Bỏ trống để lấy mọi phường.
    /// </summary>
    public int? AreaId { get; set; }

    /// <summary>
    /// Khoảng thời gian tính theo ngày tạo sự vụ. Nhận một trong các giá trị của
    /// <see cref="UrbanService.BLL.Common.Constraint.DashboardRange"/>:
    /// all, 7d, 1m, 6m, 1y. Bỏ trống tương đương all.
    /// </summary>
    public string? Range { get; set; }

    /// <summary>
    /// Số điểm tọa độ tối đa cho mỗi cặp danh mục - phường, mặc định 500,
    /// tối đa 5000.
    /// </summary>
    public int MaxPointsPerArea { get; set; } = 500;
}
