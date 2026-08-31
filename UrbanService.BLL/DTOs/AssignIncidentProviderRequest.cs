namespace UrbanService.BLL.DTOs;

public sealed class AssignIncidentProviderRequest
{
    public int CoordinatorId { get; set; }

    public string? Note { get; set; }
}
