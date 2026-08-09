namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class FeedbackStatusDistributionDto
{
    public string Status { get; set; } = null!;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}