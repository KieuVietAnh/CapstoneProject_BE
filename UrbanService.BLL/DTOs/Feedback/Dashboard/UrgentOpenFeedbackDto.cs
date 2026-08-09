namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class UrgentOpenFeedbackDto
{
    public Guid FeedbackId { get; set; }

    public string Title { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string LocationText { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? DueDate { get; set; }

    public double AgeHours { get; set; }

    public bool IsOverdue { get; set; }
}