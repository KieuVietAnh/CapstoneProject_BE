using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IFeedbackService
{
    Task<FeedbackDetailDto> CreateAsync(
        Guid userId,
        FeedbackCreateRequest request,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments,
        Guid? targetIncidentId = null);

    Task<PagedResultDto<FeedbackListItemDto>> GetMyFeedbacksAsync(Guid userId, FeedbackQueryParameters query);

    Task<PagedResultDto<FeedbackListItemDto>> GetResidentFeedFeedbacksAsync(FeedbackQueryParameters query);

    Task<FeedbackDetailDto> GetMyFeedbackDetailAsync(Guid userId, Guid feedbackId);

    Task<FeedbackDetailDto> GetResidentFeedFeedbackDetailAsync(Guid currentUserId, Guid feedbackId);

    Task<PagedResultDto<FeedbackListItemDto>> GetAllFeedbacksAsync(
        Guid currentUserId,
        FeedbackQueryParameters query);

    Task<PagedResultDto<FeedbackWithAnalysisResultDto>> GetAiReviewedFeedbacksAsync(
        Guid currentUserId,
        FeedbackQueryParameters query);

    Task<FeedbackDetailDto> GetFeedbackDetailAsync(Guid currentUserId, Guid feedbackId);

    Task<FeedbackDetailDto> UpdateAsync(Guid userId, Guid feedbackId, FeedbackUpdateRequest request);

    Task<FeedbackDetailDto> UpdateByStaffAsync(Guid currentUserId, Guid feedbackId, StaffFeedbackUpdateRequest request);

    Task DeleteAsync(Guid userId, Guid feedbackId);

    Task DeleteByManagementAsync(Guid feedbackId);

    Task<FeedbackDetailDto> AddAttachmentsAsync(
        Guid userId,
        Guid feedbackId,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments);

    Task DeleteAttachmentAsync(Guid userId, Guid feedbackId, int attachmentId);

    Task<FeedbackStatusHistoryDto> UpdateStatusByStaffOrAdminAsync(
        Guid currentUserId,
        Guid feedbackId,
        UpdateFeedbackStatusRequest request);

    Task<FeedbackCommentDto> AddCommentAsync(Guid userId, Guid feedbackId, FeedbackCommentCreateRequest request);

    Task SupportAsync(Guid userId, Guid feedbackId);

    Task UnsupportAsync(Guid userId, Guid feedbackId);

    Task VerifyFeedbackAsync(
        Guid feedbackId,
        Guid staffUserId);

    Task<IncidentProviderAssignmentDto> AssignIncidentProviderAsync(
        Guid incidentId,
        Guid staffUserId,
        AssignIncidentProviderRequest request);

    Task<IReadOnlyCollection<ProviderCandidateDto>> GetIncidentProviderCandidatesAsync(
        Guid incidentId,
        Guid currentUserId);

    Task<IncidentProviderAssignmentDto?> GetCurrentProviderAssignmentAsync(
        Guid incidentId,
        Guid currentUserId);

    Task<IncidentProviderAssignmentDto> UpdateProviderAssignmentStatusAsync(
        int providerAssignmentId,
        Guid currentUserId,
        UpdateProviderAssignmentStatusRequest request);

    Task<ProviderContactLogDto> AddProviderContactLogAsync(
        int providerAssignmentId,
        Guid currentUserId,
        ProviderContactLogCreateRequest request);

    Task<IReadOnlyCollection<ProviderContactLogDto>> GetProviderContactLogsAsync(
        int providerAssignmentId,
        Guid currentUserId);

    Task<IReadOnlyCollection<CompletionDocumentDto>> AddCompletionDocumentsAsync(
        int providerAssignmentId,
        Guid currentUserId,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> documents,
        string? description);

    Task<IReadOnlyCollection<CompletionDocumentDto>> GetCompletionDocumentsAsync(
        int providerAssignmentId,
        Guid currentUserId);

    Task<IReadOnlyCollection<FeedbackResolutionDto>> GetFeedbackResolutionsAsync(
        Guid feedbackId,
        Guid currentUserId);

    Task<IReadOnlyCollection<FeedbackResolutionDto>> GetFeedbackResolutionsAsync(Guid feedbackId);

    Task<IReadOnlyCollection<FeedbackResolutionDto>> GetIncidentResolutionsAsync(
        Guid incidentId,
        Guid currentUserId);

    Task<FeedbackResolutionDto> GetResolutionAsync(
        int resolutionId,
        Guid currentUserId);

    Task NotifyProviderResultAsync(
        Guid feedbackId,
        Guid currentUserId,
        NotifyProviderResultRequest request);

    Task SubmitIncidentResolutionAsync(
        Guid incidentId,
        Guid staffUserId,
        SubmitResolutionRequest request);

    Task ApproveResolutionAsync(
        Guid feedbackId,
        Guid managerId,
        string? note);

    Task RequireReworkAsync(
        Guid feedbackId,
        Guid managerId,
        string reason);

    Task<FeedbackResolutionReviewDto> CitizenReviewAsync(
        CitizenReviewRequest request);

    Task ClearCompletionDocumentsAsync(
        int providerAssignmentId,
        Guid currentUserId);

    Task EnsureManagementFeedbackReadAccessAsync(
        Guid feedbackId,
        Guid currentUserId);

    Task EnsureProviderAssignmentOperationAccessAsync(
        int providerAssignmentId,
        Guid currentUserId);
}
