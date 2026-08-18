namespace UrbanService.BLL.DTOs.SLA;

public class SlaStatusDto
{
    public Guid FeedbackId { get; set; }

    public long FeedbackSlaId { get; set; }


    public string Status { get; set; } = null!;


    public string ResponseStatus { get; set; } = null!;


    public string ResolutionStatus { get; set; } = null!;


    // Thời gian hiện tại của server.
    // FE dùng để đồng bộ countdown với backend.
    public DateTime ServerTime { get; set; }


    public DateTime StartedAt { get; set; }


    public DateTime ResponseDueAt { get; set; }


    public DateTime ResolutionDueAt { get; set; }


    // Giữ lại để tương thích code hiện tại.
    public int ResponseRemainingMinutes { get; set; }


    public int ResolutionRemainingMinutes { get; set; }


    // Dùng cho countdown realtime.
    public int ResponseRemainingSeconds { get; set; }


    public int ResolutionRemainingSeconds { get; set; }


    public double ResponseProgressPercent { get; set; }


    public double ResolutionProgressPercent { get; set; }


    public bool IsResponseWarning { get; set; }


    public bool IsResolutionWarning { get; set; }


    public bool IsResponseBreached { get; set; }


    public bool IsResolutionBreached { get; set; }
}