namespace UrbanService.BLL.DTOs.SLA;

public class PauseSlaRequest
{
    public string ReasonCode { get; set; } = null!;

    public string? ReasonNote { get; set; }
}