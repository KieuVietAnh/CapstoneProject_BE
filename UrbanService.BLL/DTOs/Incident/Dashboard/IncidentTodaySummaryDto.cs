namespace UrbanService.BLL.DTOs.Incident.Dashboard;

/// <summary>
/// Tình hình tiếp nhận và xử lý trong ngày hôm nay.
///
/// Ranh giới ngày tính theo giờ Việt Nam, không theo UTC, để "hôm nay" khớp với
/// ngày người dùng đang nhìn trên lịch.
/// </summary>
public class IncidentTodaySummaryDto
{
    /// <summary>
    /// Ngày đang thống kê, theo giờ Việt Nam.
    /// </summary>
    public DateOnly Date { get; set; }

    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>
    /// Mốc đầu ngày quy về UTC. Trả ra để client đối chiếu được vì sao một bản
    /// ghi rơi vào hôm nay hay hôm qua.
    /// </summary>
    public DateTime StartOfDayUtc { get; set; }

    /// <summary>
    /// Mốc cuối ngày quy về UTC, không bao gồm chính nó.
    /// </summary>
    public DateTime EndOfDayUtc { get; set; }

    /// <summary>
    /// Số phản ánh người dân gửi trong hôm nay. Phản ánh trùng nhau vẫn đếm riêng.
    /// </summary>
    public int ReportCount { get; set; }

    /// <summary>
    /// Số sự vụ mới phát sinh trong hôm nay.
    /// </summary>
    public int IncidentCount { get; set; }

    /// <summary>
    /// Số sự vụ chuyển sang trạng thái kết thúc trong hôm nay.
    /// </summary>
    public int ResolvedCount { get; set; }

    /// <summary>
    /// Phản ánh hôm nay chia theo danh mục, nhiều nhất trước.
    /// </summary>
    public IReadOnlyCollection<IncidentCategoryDistributionDto> ByCategory { get; set; }
        = Array.Empty<IncidentCategoryDistributionDto>();

    /// <summary>
    /// Phản ánh hôm nay chia theo phường, nhiều nhất trước.
    /// </summary>
    public IReadOnlyCollection<IncidentAreaCountDto> ByArea { get; set; }
        = Array.Empty<IncidentAreaCountDto>();
}
