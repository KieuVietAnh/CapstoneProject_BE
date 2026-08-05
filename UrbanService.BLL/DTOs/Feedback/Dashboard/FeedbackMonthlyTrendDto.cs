namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class FeedbackMonthlyTrendDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public string Period { get; set; } = null!;

    public int CreatedCount { get; set; }

    public int CompletedCount { get; set; }

    public int CancelledCount { get; set; }
}