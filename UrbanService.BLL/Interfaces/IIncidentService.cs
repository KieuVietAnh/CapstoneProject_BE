using UrbanService.BLL.Dtos;
using UrbanService.DAL.Entities;

namespace UrbanService.BLL.Interfaces;

public interface IIncidentService
{
    Task<Guid> StageNewReportIncidentAsync(
        Feedback feedback,
        Guid actorUserId,
        DateTime occurredAt);

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
}
