namespace UrbanService.BLL.DTOs.SLA;

public class SlaStatusDto
{
    public Guid FeedbackId { get; set; }

    public long FeedbackSlaId { get; set; }


    public string Status { get; set; } = null!;


    public string ResponseStatus { get; set; } = null!;


    public string ResolutionStatus { get; set; } = null!;


    public DateTime StartedAt { get; set; }


    public DateTime ResponseDueAt { get; set; }


    public DateTime ResolutionDueAt { get; set; }


    public int ResponseRemainingMinutes { get; set; }


    public int ResolutionRemainingMinutes { get; set; }


    public double ResponseProgressPercent { get; set; }


    public double ResolutionProgressPercent { get; set; }


    public bool IsResponseWarning { get; set; }


    public bool IsResolutionWarning { get; set; }


    public bool IsResponseBreached { get; set; }


    public bool IsResolutionBreached { get; set; }
}