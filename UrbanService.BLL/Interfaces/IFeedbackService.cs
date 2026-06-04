using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces;

public interface IFeedbackService
{
    Task<FeedbackDetailDto> CreateAsync(
        Guid userId,
        FeedbackCreateRequest request,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments);

    Task<PagedResultDto<FeedbackListItemDto>> GetMyFeedbacksAsync(Guid userId, FeedbackQueryParameters query);

    Task<FeedbackDetailDto> GetMyFeedbackDetailAsync(Guid userId, Guid feedbackId);

    Task<FeedbackDetailDto> UpdateAsync(Guid userId, Guid feedbackId, FeedbackUpdateRequest request);

    Task DeleteAsync(Guid userId, Guid feedbackId);

    Task<FeedbackDetailDto> AddAttachmentsAsync(
        Guid userId,
        Guid feedbackId,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments);

    Task DeleteAttachmentAsync(Guid userId, Guid feedbackId, int attachmentId);

    Task<FeedbackStatusHistoryDto> UpdateStatusAsync(Guid userId, Guid feedbackId, UpdateFeedbackStatusRequest request);

    Task<FeedbackCommentDto> AddCommentAsync(Guid userId, Guid feedbackId, FeedbackCommentCreateRequest request);

    Task SupportAsync(Guid userId, Guid feedbackId);

    Task UnsupportAsync(Guid userId, Guid feedbackId);
}
