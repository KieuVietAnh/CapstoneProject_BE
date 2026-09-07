namespace UrbanService.BLL.DTOs.Incident.Dashboard;

public class IncidentDashboardOverviewDto
{
    /// <summary>
    /// Số phản ánh người dân đã gửi. Đây là chỉ số tiếp nhận,
    /// nhiều phản ánh trùng nhau vẫn được đếm riêng.
    /// </summary>
    public int TotalFeedback { get; set; }

    /// <summary>
    /// Số phản ánh nhận được trong ngày hôm nay.
    /// </summary>
    public int NewToday { get; set; }

    /// <summary>
    /// Số sự vụ đang được theo dõi. Đây là số việc thực sự phải xử lý,
    /// các phản ánh trùng đã được gộp về cùng một sự vụ.
    /// </summary>
    public int TotalIncident { get; set; }

    /// <summary>
    /// Số sự vụ mới phát sinh trong ngày hôm nay.
    /// </summary>
    public int NewIncidentToday { get; set; }

    public int Assigned { get; set; }

    public int InProgress { get; set; }

    public int PendingApproval { get; set; }

    public int Completed { get; set; }

    public int Cancelled { get; set; }

    public int UrgentOpen { get; set; }

    public decimal CompletionRate { get; set; }
}