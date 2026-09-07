namespace UrbanService.BLL.DTOs.Incident.Dashboard;

public class IncidentPriorityDistributionDto
{
    public string Priority { get; set; } = null!;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}