namespace UrbanService.BLL.DTOs.SLA;

public class SlaStatusDto
{
    public Guid IncidentId { get; set; }

    public long IncidentSlaId { get; set; }


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


    /*
     * Bối cảnh tạm dừng.
     *
     * Chỉ trạng thái "Paused" thôi thì người xử lý thấy đồng hồ đứng im mà không
     * hiểu vì sao, dễ tưởng hệ thống lỗi hoặc tưởng mình được thư thả. Mấy field dưới
     * cho họ biết ai dừng, vì lý do gì và từ lúc nào, mà không phải mở màn quản lý.
     * Null khi SLA đang chạy.
     */
    public string? PauseReasonCode { get; set; }


    public string? PauseReasonNote { get; set; }


    public DateTime? PausedAt { get; set; }


    public string? PausedByUserName { get; set; }
}