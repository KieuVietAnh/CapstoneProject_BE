using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IFeedbackDuplicateCandidateService
{
    Task<FeedbackDuplicateSummaryDto> GetSummaryAsync(Guid actorUserId);

    Task<PagedResultDto<FeedbackDuplicateCandidateDto>> GetCandidatesAsync(
        FeedbackDuplicateQueryParameters query,
        Guid actorUserId);

    Task<FeedbackDuplicateCandidateDto> GetCandidateDetailAsync(
        Guid duplicateCandidateId,
        Guid actorUserId);

    Task<FeedbackDuplicateCandidateDto> ConfirmAsync(Guid duplicateCandidateId, Guid reviewerUserId);

    Task<FeedbackDuplicateCandidateDto> RejectAsync(Guid duplicateCandidateId, Guid reviewerUserId);

    Task<IReadOnlyCollection<FeedbackListItemDto>> GetLinkedFeedbacksAsync(Guid feedbackId);

    Task<RelatedFeedbacksDto> GetRelatedFeedbacksAsync(Guid feedbackId);
}
