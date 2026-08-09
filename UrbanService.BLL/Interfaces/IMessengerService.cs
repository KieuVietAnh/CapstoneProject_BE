using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IMessengerService
{
    bool IsVerificationRequestValid(string? mode, string? verifyToken);

    bool IsSignatureValid(string payload, string? signature);

    Task ProcessWebhookAsync(string payload, CancellationToken cancellationToken = default);

    Task<MessengerConversationDto?> GetConversationAsync(
        string senderPsid,
        CancellationToken cancellationToken = default);

    Task<MessengerConversationDto> ResetConversationAsync(
        string senderPsid,
        CancellationToken cancellationToken = default);
}
