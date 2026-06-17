using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IFeedbackService
{
    Task<FeedbackDetailDto> CreateAsync(
        Guid userId,
        FeedbackCreateRequest request,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments);

    Task<PagedResultDto<FeedbackListItemDto>> GetMyFeedbacksAsync(Guid userId, FeedbackQueryParameters query);

    Task<FeedbackDetailDto> GetMyFeedbackDetailAsync(Guid userId, Guid feedbackId);

    Task<PagedResultDto<FeedbackListItemDto>> GetAllFeedbacksAsync(FeedbackQueryParameters query);

    Task<FeedbackDetailDto> GetFeedbackDetailAsync(Guid currentUserId, Guid feedbackId);

    Task<FeedbackDetailDto> UpdateAsync(Guid userId, Guid feedbackId, FeedbackUpdateRequest request);

    Task<FeedbackDetailDto> UpdateByStaffAsync(Guid currentUserId, Guid feedbackId, StaffFeedbackUpdateRequest request);

    Task DeleteAsync(Guid userId, Guid feedbackId);

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

    Task AssignFeedbackAsync(
        AssignFeedbackRequest request);


    Task SubmitResolutionAsync(
        SubmitResolutionRequest request);

    Task ApproveResolutionAsync(
        Guid feedbackId,
        Guid managerId,
        string? note);

    Task RequireReworkAsync(
        Guid feedbackId,
        Guid managerId,
        string reason);

    Task CitizenReviewAsync(
        CitizenReviewRequest request);
}
