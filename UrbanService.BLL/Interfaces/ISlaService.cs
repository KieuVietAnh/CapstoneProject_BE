using UrbanService.BLL.DTOs.SLA;

namespace UrbanService.BLL.Interfaces;

public interface ISlaService
{
    /// <summary>
    /// Bắt đầu SLA cho feedback đã được staff xác minh.
    /// </summary>
    Task<FeedbackSlaDto> StartAsync(
        Guid feedbackId,
        Guid startedByUserId);

    /// <summary>
    /// Lấy SLA hiện tại của feedback.
    /// </summary>-
    Task<FeedbackSlaDto> GetCurrentByFeedbackIdAsync(
        Guid feedbackId);

    /// <summary>
    /// Lấy chi tiết SLA theo ID.
    /// </summary>
    Task<FeedbackSlaDto> GetByIdAsync(
        long feedbackSlaId);

    /// <summary>
    /// Đánh dấu feedback đã có phản hồi đầu tiên.
    /// </summary>
    Task<FeedbackSlaDto> MarkRespondedAsync(
        Guid feedbackId,
        Guid triggeredByUserId,
        string? note);

    /// <summary>
    /// Tạm dừng SLA.
    /// </summary>
    Task<FeedbackSlaDto> PauseAsync(
        Guid feedbackId,
        Guid pausedByUserId,
        PauseSlaRequest request);

    /// <summary>
    /// Tiếp tục SLA đang tạm dừng.
    /// </summary>
    Task<FeedbackSlaDto> ResumeAsync(
        Guid feedbackId,
        Guid resumedByUserId,
        ResumeSlaRequest request);

    /// <summary>
    /// Hoàn thành SLA khi feedback đã xử lý xong.
    /// </summary>
    Task<FeedbackSlaDto> CompleteAsync(
        Guid feedbackId,
        Guid completedByUserId,
        CompleteSlaRequest request);

    /// <summary>
    /// Hủy SLA hiện tại.
    /// </summary>
    Task<FeedbackSlaDto> CancelAsync(
        Guid feedbackId,
        Guid cancelledByUserId,
        string? note);

    /// <summary>
    /// Chọn lại policy và tính lại deadline của SLA.
    /// </summary>
    Task<FeedbackSlaDto> RecalculateAsync(
        Guid feedbackId,
        Guid recalculatedByUserId,
        RecalculateSlaRequest request);

    /// <summary>
    /// Kiểm tra và cập nhật trạng thái vi phạm của một SLA.
    /// </summary>
    Task CheckViolationAsync(long feedbackSlaId);

    /// <summary>
    /// Kiểm tra tất cả SLA đang chạy.
    /// Trả về số SLA vừa được cập nhật vi phạm.
    /// </summary>
    Task<int> CheckAllRunningSlasAsync();
}