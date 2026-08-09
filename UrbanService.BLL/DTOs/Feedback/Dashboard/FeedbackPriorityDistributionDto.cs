namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class FeedbackPriorityDistributionDto
{
    public string Priority { get; set; } = null!;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}