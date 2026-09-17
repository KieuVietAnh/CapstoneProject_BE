namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Kết quả thống kê sự vụ theo danh mục và phường, kèm bộ lọc đã áp dụng.
///
/// Các số đếm ở cấp này là tổng của toàn bộ kết quả sau khi lọc, dùng cho thẻ
/// tóm tắt phía trên bản đồ mà không cần client tự cộng lại.
/// </summary>
public class IncidentDistributionReportDto
{
    public IncidentDistributionFilterDto Filter { get; set; } = new();

    public int TotalCount { get; set; }

    public int OpenCount { get; set; }

    public int CompletedCount { get; set; }

    /// <summary>
    /// Tổng số sự vụ có đủ tọa độ trong kết quả. Nhỏ hơn
    /// <see cref="TotalCount"/> khi có sự vụ chưa gắn tọa độ.
    /// </summary>
    public int MappedCount { get; set; }

    /// <summary>
    /// Các danh mục có sự vụ, nhiều nhất trước.
    /// </summary>
    public IReadOnlyCollection<IncidentCategoryBreakdownDto> Categories { get; set; }
        = Array.Empty<IncidentCategoryBreakdownDto>();
}
