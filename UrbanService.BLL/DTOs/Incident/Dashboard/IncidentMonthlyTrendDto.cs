namespace UrbanService.BLL.DTOs.Incident.Dashboard;

public class IncidentMonthlyTrendDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string Period { get; set; } = null!;

    /// <summary>
    /// Số phản ánh người dân gửi trong tháng. Chỉ số tiếp nhận.
    /// </summary>
    public int ReportCount { get; set; }

    /// <summary>
    /// Số sự vụ mới phát sinh trong tháng.
    /// </summary>
    public int CreatedCount { get; set; }

    public int CompletedCount { get; set; }

    public int CancelledCount { get; set; }
}