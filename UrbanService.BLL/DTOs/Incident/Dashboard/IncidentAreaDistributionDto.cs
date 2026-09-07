namespace UrbanService.BLL.DTOs.Incident.Dashboard;

public class IncidentAreaDistributionDto
{
    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public int Count { get; set; }

    public int OpenCount { get; set; }

    public int CompletedCount { get; set; }

    public decimal Percentage { get; set; }

    /// <summary>
    /// Tâm khu vực theo cấu hình `OperatingArea`. Dùng để canh khung nhìn bản đồ
    /// khi khu vực chưa có sự vụ nào có tọa độ.
    /// </summary>
    public decimal? CenterLatitude { get; set; }

    public decimal? CenterLongitude { get; set; }

    /// <summary>
    /// Tổng số sự vụ của khu vực có đủ tọa độ. Nếu lớn hơn số phần tử trong
    /// <see cref="Points"/> thì danh sách đã bị cắt bớt theo giới hạn yêu cầu.
    /// </summary>
    public int MappedCount { get; set; }

    /// <summary>
    /// Tọa độ các sự vụ trong khu vực, sắp xếp theo thời điểm tạo mới nhất trước.
    /// </summary>
    public IReadOnlyCollection<IncidentMapPointDto> Points { get; set; }
        = Array.Empty<IncidentMapPointDto>();
}
