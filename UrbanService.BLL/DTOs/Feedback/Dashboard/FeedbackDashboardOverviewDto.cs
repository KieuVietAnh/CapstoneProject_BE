namespace UrbanService.BLL.DTOs.Feedback.Dashboard;

public class FeedbackDashboardOverviewDto
{
    public int TotalFeedback { get; set; }

    public int NewToday { get; set; }

    public int Assigned { get; set; }

    public int InProgress { get; set; }

    public int PendingApproval { get; set; }

    public int Completed { get; set; }

    public int Cancelled { get; set; }

    public int UrgentOpen { get; set; }

    public decimal CompletionRate { get; set; }
}