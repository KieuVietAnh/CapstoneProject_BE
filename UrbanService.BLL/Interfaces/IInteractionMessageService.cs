using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IInteractionMessageService
{
    Task<IReadOnlyCollection<InteractionMessageDto>> GetTicketMessagesAsync(Guid currentUserId, Guid feedbackId, bool includeInternal = false);

    Task<InteractionMessageDto> SendMessageAsync(Guid currentUserId, Guid feedbackId, InteractionMessageCreateRequest request);

    Task<InteractionMessageDto> AddSystemMessageAsync(Guid currentUserId, Guid feedbackId, SystemInteractionMessageCreateRequest request);

    /// <summary>
    /// Kiểm tra quyền đọc hội thoại của một ticket và cho biết người dùng có được
    /// nhận ghi chú nội bộ không.
    ///
    /// Hub realtime gọi hàm này trước khi cho client vào group, để luật phân quyền
    /// chỉ tồn tại một nơi thay vì bị chép lại ở lớp hạ tầng. Ném
    /// <see cref="Common.ForbiddenAccessException"/> khi không có quyền.
    /// </summary>
    Task<InteractionConversationAccessDto> EnsureConversationAccessAsync(Guid currentUserId, Guid feedbackId);
}