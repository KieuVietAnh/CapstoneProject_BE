namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class FeedbackAreaDistributionDto
{
    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public int Count { get; set; }

    public int OpenCount { get; set; }

    public int CompletedCount { get; set; }

    public decimal Percentage { get; set; }
}