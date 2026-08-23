using UrbanService.BLL.Dtos;
using UrbanService.DAL.Entities;

namespace UrbanService.BLL.Interfaces;

public interface IIncidentService
{
    Task<Guid> StageNewReportIncidentAsync(
        Feedback feedback,
        Guid actorUserId,
        DateTime occurredAt);

    Task StageReportInExistingIncidentAsync(
        Feedback feedback,
        Guid incidentId,
        Guid actorUserId,
        DateTime occurredAt,
        CancellationToken cancellationToken = default);

    Task<Guid> RelinkConfirmedDuplicateAsync(
        Feedback childFeedback,
        Feedback parentFeedback,
        Guid staffUserId,
        decimal? confidenceScore,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<IncidentListItemDto>> GetIncidentsAsync(
        IncidentQueryParameters query,
        CancellationToken cancellationToken = default);

    Task<IncidentDetailDto> GetIncidentDetailAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<IncidentDetailDto> LinkReportAsync(
        Guid incidentId,
        LinkIncidentReportRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task UnlinkReportAsync(
        Guid incidentId,
        Guid feedbackId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<PublicIncidentListItemDto>> GetPublicIncidentsAsync(
        IncidentQueryParameters query,
        CancellationToken cancellationToken = default);

    Task<PublicIncidentDetailDto> GetPublicIncidentDetailAsync(
        Guid incidentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PublicIncidentReportDto>> GetPublicIncidentReportsAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<PublicIncidentEventDto>> GetPublicTimelineAsync(
        Guid incidentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<IncidentEventDto>> GetManagementTimelineAsync(
        Guid incidentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<IncidentListItemDto>> GetMyIncidentsAsync(
        Guid userId,
        IncidentQueryParameters query,
        CancellationToken cancellationToken = default);

    Task SubscribeAsync(Guid incidentId, Guid userId, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(Guid incidentId, Guid userId, CancellationToken cancellationToken = default);

    Task<IncidentDetailDto> UpdateIncidentAsync(
        Guid incidentId,
        UpdateIncidentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<IncidentDetailDto> UpdateStatusAsync(
        Guid incidentId,
        UpdateIncidentStatusRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<IncidentDetailDto> AssignAsync(
        Guid incidentId,
        AssignIncidentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IncidentAssigneeCandidateDto>> GetAssigneeCandidatesAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    Task<IncidentDetailDto> MergeAsync(
        Guid sourceIncidentId,
        MergeIncidentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
