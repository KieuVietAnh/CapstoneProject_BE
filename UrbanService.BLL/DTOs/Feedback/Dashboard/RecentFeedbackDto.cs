namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class RecentFeedbackDto
{
    public Guid FeedbackId { get; set; }

    public string Title { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Priority { get; set; }

    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string LocationText { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}