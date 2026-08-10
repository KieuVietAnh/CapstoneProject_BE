using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IZaloService
{
    bool IsSignatureValid(string payload, string? signature);

    Task ProcessWebhookAsync(string payload, CancellationToken cancellationToken = default);

    Task<ZaloConversationDto?> GetConversationAsync(
        string senderUserId,
        CancellationToken cancellationToken = default);

    Task<ZaloConversationDto> ResetConversationAsync(
        string senderUserId,
        CancellationToken cancellationToken = default);
}
