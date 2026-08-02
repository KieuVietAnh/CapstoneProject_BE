namespace UrbanService.BLL.DTOs.SLA;

public class RecalculateSlaRequest
{
    public int? CategoryId { get; set; }

    public string? Priority { get; set; }

    public string? Note { get; set; }
}