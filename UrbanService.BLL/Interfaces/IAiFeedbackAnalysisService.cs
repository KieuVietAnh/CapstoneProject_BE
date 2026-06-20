using UrbanService.BLL.DTOs.AI;

namespace UrbanService.BLL.Interfaces;

public interface IAiFeedbackAnalysisService
{
    Task<AiAnalysisResponseDto> AnalyzeFeedbackAsync(
        Guid feedbackId,
        Guid reviewedByUserId,
        CancellationToken cancellationToken = default);
}
