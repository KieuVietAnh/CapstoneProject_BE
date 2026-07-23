using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IFeedbackDuplicateCandidateService
{
    Task<FeedbackDuplicateSummaryDto> GetSummaryAsync();

    Task<PagedResultDto<FeedbackDuplicateCandidateDto>> GetCandidatesAsync(FeedbackDuplicateQueryParameters query);

    Task<FeedbackDuplicateCandidateDto> GetCandidateDetailAsync(Guid duplicateCandidateId);

    Task<FeedbackDuplicateCandidateDto> ConfirmAsync(Guid duplicateCandidateId, Guid staffUserId);

    Task<FeedbackDuplicateCandidateDto> RejectAsync(Guid duplicateCandidateId, Guid staffUserId);

    Task<IReadOnlyCollection<FeedbackListItemDto>> GetLinkedFeedbacksAsync(Guid feedbackId);

    Task<RelatedFeedbacksDto> GetRelatedFeedbacksAsync(Guid feedbackId);
}