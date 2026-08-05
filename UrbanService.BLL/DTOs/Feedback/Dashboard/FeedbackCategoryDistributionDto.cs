namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class FeedbackCategoryDistributionDto
{
    public int? CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}