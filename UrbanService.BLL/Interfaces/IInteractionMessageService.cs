using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IInteractionMessageService
{
    Task<IReadOnlyCollection<InteractionMessageDto>> GetTicketMessagesAsync(Guid currentUserId, Guid feedbackId, bool includeInternal = false);

    Task<InteractionMessageDto> SendMessageAsync(Guid currentUserId, Guid feedbackId, InteractionMessageCreateRequest request);

    Task<InteractionMessageDto> AddSystemMessageAsync(Guid currentUserId, Guid feedbackId, SystemInteractionMessageCreateRequest request);
}