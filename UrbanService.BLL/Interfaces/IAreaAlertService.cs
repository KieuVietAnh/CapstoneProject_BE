using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces;

public interface IAreaAlertService
{
    Task<UserAreaAlertDto> CreateManualAlertAsync(Guid createdByUserId, CreateAreaAlertRequest request);

    Task<UserAreaAlertDto> CreateAlertFromFeedbackAsync(
        Guid createdByUserId,
        Guid feedbackId,
        CreateAreaAlertFromFeedbackRequest request);

    Task<PagedResultDto<UserAreaAlertDto>> GetAlertsAsync(Guid userId, AreaAlertQueryParameters query);

    Task<IReadOnlyCollection<UserAreaSubscriptionDto>> GetMySubscriptionsAsync(Guid userId);

    Task<UserAreaSubscriptionDto> CreateSubscriptionAsync(Guid userId, CreateAreaSubscriptionRequest request);

    Task DeleteSubscriptionAsync(Guid userId, int areaId);
}