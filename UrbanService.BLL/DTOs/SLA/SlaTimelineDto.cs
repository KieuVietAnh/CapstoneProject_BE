namespace UrbanService.BLL.DTOs.SLA;

public class SlaTimelineDto
{
    public long SlaEventId { get; set; }

    public string EventType { get; set; } = null!;

    public string? OldStatus { get; set; }

    public string? NewStatus { get; set; }

    public string? Note { get; set; }

    public string TriggerSource { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}