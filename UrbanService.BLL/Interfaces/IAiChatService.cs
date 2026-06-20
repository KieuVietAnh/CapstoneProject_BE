using UrbanService.BLL.DTOs.AI;

namespace UrbanService.BLL.Interfaces;

public interface IAiChatService
{
    Task<AiChatResponse> SendAsync(
        Guid userId,
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}
