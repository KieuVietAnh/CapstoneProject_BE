using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

/// <summary>
/// Đẩy tin nhắn trao đổi của một ticket xuống các client đang mở hội thoại đó.
///
/// Tách thành interface ở BLL để business service không phụ thuộc vào SignalR;
/// lớp API giữ phần hạ tầng, giống cách <see cref="IRealtimeNotificationSender"/>
/// đang làm cho thông báo.
/// </summary>
public interface IRealtimeInteractionMessageSender
{
    /// <summary>
    /// Phát tin nhắn vừa lưu. Ghi chú nội bộ chỉ được gửi cho nhóm nhân sự, không
    /// bao giờ xuống người dân, nên phần quyết định nằm ở implementation chứ không
    /// phải ở nơi gọi.
    /// </summary>
    Task SendTicketMessageAsync(
        Guid feedbackId,
        InteractionMessageDto message,
        CancellationToken cancellationToken = default);
}
