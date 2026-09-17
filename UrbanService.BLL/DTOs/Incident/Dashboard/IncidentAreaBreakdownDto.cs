namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Một phường trong phạm vi của một danh mục, kèm tọa độ các sự vụ thuộc đúng
/// cặp danh mục - phường đó.
/// </summary>
public class IncidentAreaBreakdownDto
{
    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public string? WardCode { get; set; }

    public string? DistrictName { get; set; }

    /// <summary>
    /// Tâm phường theo cấu hình `OperatingArea`. Dùng để canh khung nhìn bản đồ
    /// khi cặp danh mục - phường này chưa có sự vụ nào có tọa độ.
    /// </summary>
    public decimal? CenterLatitude { get; set; }

    public decimal? CenterLongitude { get; set; }

    public int Count { get; set; }

    public int OpenCount { get; set; }

    public int CompletedCount { get; set; }

    /// <summary>
    /// Tỷ lệ của phường này trong nội bộ danh mục đang xét, không phải tỷ lệ
    /// trên toàn bộ sự vụ.
    /// </summary>
    public decimal PercentageInCategory { get; set; }

    /// <summary>
    /// Tổng số sự vụ của cặp danh mục - phường có đủ tọa độ. Nếu lớn hơn số
    /// phần tử trong <see cref="Points"/> thì danh sách đã bị cắt bớt.
    /// </summary>
    public int MappedCount { get; set; }

    /// <summary>
    /// Tọa độ các sự vụ, sắp xếp theo thời điểm tạo mới nhất trước.
    /// </summary>
    public IReadOnlyCollection<IncidentMapPointDto> Points { get; set; }
        = Array.Empty<IncidentMapPointDto>();
}
