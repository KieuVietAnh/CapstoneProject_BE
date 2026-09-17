namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Phân bố sự vụ theo danh mục, mỗi danh mục tách tiếp theo từng phường và kèm
/// tọa độ để chấm lên bản đồ.
///
/// Khác với `category-distribution` chỉ trả về số đếm theo danh mục, và khác với
/// `area-distribution` chỉ gom theo phường: ở đây một sự vụ được quy về đúng một
/// ô (danh mục, phường), nên tổng số đếm của các ô bằng tổng số sự vụ.
/// </summary>
public class IncidentCategoryAreaDistributionDto
{
    public int? CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public int Count { get; set; }

    public int OpenCount { get; set; }

    public int CompletedCount { get; set; }

    /// <summary>
    /// Tỷ lệ của danh mục trên tổng số sự vụ trong phạm vi đọc của người dùng.
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Tổng số sự vụ của danh mục có đủ tọa độ, cộng dồn từ các phường.
    /// </summary>
    public int MappedCount { get; set; }

    /// <summary>
    /// Các phường có sự vụ thuộc danh mục này, nhiều nhất trước.
    /// </summary>
    public IReadOnlyCollection<IncidentAreaBreakdownDto> Areas { get; set; }
        = Array.Empty<IncidentAreaBreakdownDto>();
}
