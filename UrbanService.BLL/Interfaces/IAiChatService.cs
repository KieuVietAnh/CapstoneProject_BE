using UrbanService.BLL.DTOs.AI;

namespace UrbanService.BLL.Interfaces;

public interface IAiChatService
{
    Task<IReadOnlyCollection<AiConversationDto>> GetMyConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AiMessageDto>> GetConversationMessagesAsync(
        Guid userId,
        int conversationId,
        CancellationToken cancellationToken = default);

    Task<AiChatResponse> SendAsync(
        Guid userId,
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}
