namespace UrbanService.BLL.DTOs.Incident.Dashboard;

public class IncidentCategoryDistributionDto
{
    public int? CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public int Count { get; set; }

    public decimal Percentage { get; set; }
}