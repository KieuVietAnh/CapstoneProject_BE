using UrbanService.BLL.DTOs.SLA;

namespace UrbanService.BLL.Interfaces;

public interface ISlaService
{
    /// <summary>
    /// Bắt đầu SLA cho sự vụ đã được xác minh.
    /// </summary>
    Task<IncidentSlaDto> StartAsync(
        Guid incidentId,
        Guid startedByUserId);

    /// <summary>
    /// Lấy SLA hiện tại của sự vụ.
    /// </summary>
    Task<IncidentSlaDto> GetCurrentByIncidentIdAsync(
        Guid incidentId,
        Guid actorUserId);

    /// <summary>
    /// Đánh dấu sự vụ đã có phản hồi đầu tiên.
    /// </summary>
    Task<IncidentSlaDto> MarkRespondedAsync(
        Guid incidentId,
        Guid triggeredByUserId,
        string? note);

    /// <summary>
    /// Tạm dừng SLA.
    /// </summary>
    Task<IncidentSlaDto> PauseAsync(
        Guid incidentId,
        Guid pausedByUserId,
        PauseSlaRequest request);

    /// <summary>
    /// Tiếp tục SLA đang tạm dừng.
    /// </summary>
    Task<IncidentSlaDto> ResumeAsync(
        Guid incidentId,
        Guid resumedByUserId,
        ResumeSlaRequest request);

    /// <summary>
    /// Hoàn thành SLA khi sự vụ đã xử lý xong.
    /// </summary>
    Task<IncidentSlaDto> CompleteAsync(
        Guid incidentId,
        Guid completedByUserId,
        CompleteSlaRequest request);

    /// <summary>
    /// Hủy SLA hiện tại.
    /// </summary>
    Task<IncidentSlaDto> CancelAsync(
        Guid incidentId,
        Guid cancelledByUserId,
        string? note);

    /// <summary>
    /// Chọn lại policy và tính lại deadline của SLA.
    /// </summary>
    Task<IncidentSlaDto> RecalculateAsync(
        Guid incidentId,
        Guid recalculatedByUserId,
        RecalculateSlaRequest request);

    /// <summary>
    /// Kiểm tra và cập nhật trạng thái vi phạm của một SLA.
    /// </summary>
    Task CheckViolationAsync(
        long incidentSlaId,
        Guid actorUserId);

    /// <summary>
    /// Kiểm tra tất cả SLA đang chạy.
    /// Trả về số SLA vừa được cập nhật vi phạm.
    /// </summary>
    Task<int> CheckAllRunningSlasAsync();

    Task<SlaStatusDto> GetStatusAsync(
        Guid incidentId,
        Guid actorUserId);

    Task<List<SlaTimelineDto>> GetTimelineAsync(
        Guid incidentId,
        Guid actorUserId);

    /// <summary>
    /// Đồng bộ vòng đời SLA theo trạng thái sự vụ.
    /// Được gọi sau khi trạng thái Incident đã commit.
    /// </summary>
    Task SynchronizeByIncidentStatusAsync(
        Guid incidentId,
        string oldStatus,
        string newStatus,
        Guid triggeredByUserId,
        string? note);
}
