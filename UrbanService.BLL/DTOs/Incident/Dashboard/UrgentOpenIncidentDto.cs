namespace UrbanService.BLL.DTOs.Incident.Dashboard;

public class UrgentOpenIncidentDto
{
    public Guid IncidentId { get; set; }

    public string Title { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string LocationText { get; set; } = null!;

    /// <summary>
    /// Số phản ánh đang được liên kết vào sự vụ này.
    /// </summary>
    public int ReportCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DueDate { get; set; }

    public double AgeHours { get; set; }

    public bool IsOverdue { get; set; }
}