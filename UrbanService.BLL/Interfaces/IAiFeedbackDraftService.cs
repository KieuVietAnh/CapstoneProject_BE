using UrbanService.BLL.DTOs.AI;

namespace UrbanService.BLL.Interfaces;

public interface IAiFeedbackDraftService
{
    Task<AiFeedbackDraftResponse> CreateDraftAsync(
        Guid userId,
        AiFeedbackDraftRequest request,
        CancellationToken cancellationToken = default);
}