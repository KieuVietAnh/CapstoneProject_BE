using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IMessengerAccountLinkService
{
    Task<Guid?> GetLinkedUserIdAsync(
        string pageId,
        string senderPsid,
        CancellationToken cancellationToken = default);

    Task<string> CreateLinkTokenAsync(
        string pageId,
        string senderPsid,
        CancellationToken cancellationToken = default);

    Task<MessengerAccountLinkDto> ConfirmAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessengerAccountLinkDto>> GetMyLinksAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid userId,
        long linkId,
        CancellationToken cancellationToken = default);
}
