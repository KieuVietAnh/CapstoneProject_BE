namespace UrbanService.BLL.DTOs.Incident.Dashboard;

public class IncidentStatusDistributionDto
{
    public string Status { get; set; } = null!;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}