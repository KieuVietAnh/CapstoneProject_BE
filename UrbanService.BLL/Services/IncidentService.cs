using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public sealed class IncidentService : IIncidentService
{
    private const int MaxPageSize = 100;
    private readonly IUnitOfWork _uow;

    public IncidentService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<Guid> StageNewReportIncidentAsync(
        Feedback feedback,
        Guid actorUserId,
        DateTime occurredAt)
    {
        if (feedback.FeedbackId == Guid.Empty)
        {
            throw new Exception("Feedback phải có định danh trước khi tạo Incident.");
        }

        var incidentId = Guid.NewGuid();
        var linkId = Guid.NewGuid();

        await _uow.GetRepository<Incident>().AddAsync(new Incident
        {
            IncidentId = incidentId,
            AreaId = feedback.AreaId,
            CategoryId = feedback.CategoryId,
            Title = feedback.Title,
            Description = feedback.Description,
            LocationText = feedback.LocationText,
            Latitude = feedback.Latitude,
            Longitude = feedback.Longitude,
            Priority = feedback.Priority,
            Status = feedback.Status,
            DueDate = feedback.DueDate,
            CreatedAt = occurredAt,
            UpdatedAt = occurredAt
        });

        await _uow.GetRepository<IncidentReportLink>().AddAsync(new IncidentReportLink
        {
            IncidentReportLinkId = linkId,
            IncidentId = incidentId,
            FeedbackId = feedback.FeedbackId,
            LinkStatus = IncidentLinkStatus.Active,
            LinkMethod = IncidentLinkMethod.Created,
            LinkRole = IncidentLinkRole.Primary,
            Reason = "Incident created from a new report.",
            LinkedByUserId = actorUserId,
            LinkedAt = occurredAt
        });

        await _uow.GetRepository<IncidentSubscription>().AddAsync(new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = incidentId,
            UserId = feedback.UserId,
            SourceType = IncidentSubscriptionSource.Report,
            SourceFeedbackId = feedback.FeedbackId,
            IsActive = true,
            CreatedAt = occurredAt
        });

        await _uow.GetRepository<IncidentEvent>().AddRangeAsync(
        [
            new IncidentEvent
            {
                IncidentId = incidentId,
                FeedbackId = feedback.FeedbackId,
                EventType = IncidentEventType.IncidentCreated,
                ActorUserId = actorUserId,
                PayloadJson = JsonSerializer.Serialize(new { feedbackId = feedback.FeedbackId }),
                CreatedAt = occurredAt
            },
            new IncidentEvent
            {
                IncidentId = incidentId,
                FeedbackId = feedback.FeedbackId,
                EventType = IncidentEventType.ReportLinked,
                ActorUserId = actorUserId,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    incidentReportLinkId = linkId,
                    method = IncidentLinkMethod.Created,
                    role = IncidentLinkRole.Primary
                }),
                CreatedAt = occurredAt
            }
        ]);

        return incidentId;
    }

    public async Task<Guid> RelinkConfirmedDuplicateAsync(
        Feedback childFeedback,
        Feedback parentFeedback,
        Guid staffUserId,
        decimal? confidenceScore,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ValidateConfidence(confidenceScore);

        var linkRepository = _uow.GetRepository<IncidentReportLink>();
        var activeLinks = await linkRepository.Entities
            .Include(link => link.Incident)
            .Where(link =>
                (link.FeedbackId == childFeedback.FeedbackId ||
                 link.FeedbackId == parentFeedback.FeedbackId) &&
                link.LinkStatus == IncidentLinkStatus.Active)
            .ToListAsync(cancellationToken);

        var parentLink = activeLinks.FirstOrDefault(link => link.FeedbackId == parentFeedback.FeedbackId);
        Guid targetIncidentId;

        if (parentLink == null)
        {
            targetIncidentId = await StageNewReportIncidentAsync(
                parentFeedback,
                staffUserId,
                DateTime.UtcNow);
            await _uow.SaveAsync();
        }
        else
        {
            targetIncidentId = parentLink.IncidentId;
        }

        var childLink = activeLinks.FirstOrDefault(link => link.FeedbackId == childFeedback.FeedbackId);
        if (childLink?.IncidentId == targetIncidentId)
        {
            return targetIncidentId;
        }

        var now = DateTime.UtcNow;
        Guid? previousIncidentId = null;

        if (childLink != null)
        {
            previousIncidentId = childLink.IncidentId;
            childLink.LinkStatus = IncidentLinkStatus.Unlinked;
            childLink.UnlinkedByUserId = staffUserId;
            childLink.UnlinkedAt = now;
            childLink.UpdatedAt = now;
            childLink.Reason = AppendReason(childLink.Reason, "Relinked after duplicate confirmation.");

            await _uow.GetRepository<IncidentEvent>().AddAsync(new IncidentEvent
            {
                IncidentId = childLink.IncidentId,
                FeedbackId = childFeedback.FeedbackId,
                EventType = IncidentEventType.ReportUnlinked,
                ActorUserId = staffUserId,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    targetIncidentId,
                    reason = "Duplicate confirmed"
                }),
                CreatedAt = now
            });

            // Flush the old active link before inserting the replacement so the
            // partial unique index on feedback_id is never violated.
            await _uow.SaveAsync();
        }

        var targetHasActiveReports = await linkRepository.Entities
            .AnyAsync(link =>
                link.IncidentId == targetIncidentId &&
                link.LinkStatus == IncidentLinkStatus.Active,
                cancellationToken);

        var newLinkId = Guid.NewGuid();
        await linkRepository.AddAsync(new IncidentReportLink
        {
            IncidentReportLinkId = newLinkId,
            IncidentId = targetIncidentId,
            FeedbackId = childFeedback.FeedbackId,
            LinkStatus = IncidentLinkStatus.Active,
            LinkMethod = IncidentLinkMethod.StaffConfirmed,
            LinkRole = targetHasActiveReports ? IncidentLinkRole.Corroborating : IncidentLinkRole.Primary,
            ConfidenceScore = confidenceScore,
            Reason = NormalizeOptional(reason) ?? "Linked after duplicate confirmation.",
            LinkedByUserId = staffUserId,
            LinkedAt = now
        });

        await EnsureSubscriptionAsync(
            targetIncidentId,
            childFeedback.UserId,
            childFeedback.FeedbackId,
            now,
            cancellationToken);

        await _uow.GetRepository<IncidentEvent>().AddAsync(new IncidentEvent
        {
            IncidentId = targetIncidentId,
            FeedbackId = childFeedback.FeedbackId,
            EventType = IncidentEventType.ReportLinked,
            ActorUserId = staffUserId,
            PayloadJson = JsonSerializer.Serialize(new
            {
                incidentReportLinkId = newLinkId,
                method = IncidentLinkMethod.StaffConfirmed,
                confidenceScore,
                reason
            }),
            CreatedAt = now
        });

        if (previousIncidentId.HasValue && previousIncidentId.Value != targetIncidentId)
        {
            var previousHasActiveReports = await linkRepository.Entities
                .AnyAsync(link =>
                    link.IncidentId == previousIncidentId.Value &&
                    link.LinkStatus == IncidentLinkStatus.Active,
                    cancellationToken);

            if (!previousHasActiveReports)
            {
                var previousIncident = childLink?.Incident ?? await _uow.GetRepository<Incident>().Entities
                    .FirstAsync(incident => incident.IncidentId == previousIncidentId.Value, cancellationToken);
                previousIncident.Status = "Merged";
                previousIncident.MergedIntoIncidentId = targetIncidentId;
                previousIncident.UpdatedAt = now;

                var previousSubscriptions = await _uow.GetRepository<IncidentSubscription>().Entities
                    .Where(subscription =>
                        subscription.IncidentId == previousIncident.IncidentId &&
                        subscription.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var subscription in previousSubscriptions)
                {
                    subscription.IsActive = false;
                    subscription.UpdatedAt = now;
                }

                await _uow.GetRepository<IncidentEvent>().AddAsync(new IncidentEvent
                {
                    IncidentId = previousIncident.IncidentId,
                    EventType = IncidentEventType.IncidentMerged,
                    ActorUserId = staffUserId,
                    PayloadJson = JsonSerializer.Serialize(new { mergedIntoIncidentId = targetIncidentId }),
                    CreatedAt = now
                });
            }
        }

        await _uow.SaveAsync();
        return targetIncidentId;
    }

    public async Task<PagedResultDto<IncidentListItemDto>> GetIncidentsAsync(
        IncidentQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var status = NormalizeOptional(query.Status)?.ToLower();
        var priority = NormalizeOptional(query.Priority)?.ToLower();
        var search = NormalizeOptional(query.Search)?.ToLower();

        var incidents = _uow.GetRepository<Incident>().Entities.AsNoTracking();

        if (!query.IncludeMerged)
        {
            incidents = incidents.Where(incident => incident.MergedIntoIncidentId == null);
        }

        if (query.AreaId.HasValue)
        {
            incidents = incidents.Where(incident => incident.AreaId == query.AreaId.Value);
        }

        if (query.CategoryId.HasValue)
        {
            incidents = incidents.Where(incident => incident.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            incidents = incidents.Where(incident => incident.Status.ToLower() == status);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            incidents = incidents.Where(incident => incident.Priority != null && incident.Priority.ToLower() == priority);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            incidents = incidents.Where(incident =>
                incident.Title.ToLower().Contains(search) ||
                (incident.Description != null && incident.Description.ToLower().Contains(search)) ||
                incident.LocationText.ToLower().Contains(search));
        }

        var totalItems = await incidents.CountAsync(cancellationToken);
        var items = await incidents
            .OrderByDescending(incident => incident.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(incident => new IncidentListItemDto
            {
                IncidentId = incident.IncidentId,
                AreaId = incident.AreaId,
                AreaName = incident.Area.AreaName,
                CategoryId = incident.CategoryId,
                CategoryName = incident.Category != null ? incident.Category.CategoryName : null,
                Title = incident.Title,
                LocationText = incident.LocationText,
                Latitude = incident.Latitude,
                Longitude = incident.Longitude,
                Priority = incident.Priority,
                Status = incident.Status,
                MergedIntoIncidentId = incident.MergedIntoIncidentId,
                ReportCount = incident.IncidentReportLinks.Count(link => link.LinkStatus == IncidentLinkStatus.Active),
                SubscriberCount = incident.IncidentSubscriptions.Count(subscription => subscription.IsActive),
                CreatedAt = incident.CreatedAt,
                UpdatedAt = incident.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<IncidentListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<IncidentDetailDto> GetIncidentDetailAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var detail = await _uow.GetRepository<Incident>().Entities
            .AsNoTracking()
            .Where(incident => incident.IncidentId == incidentId)
            .Select(incident => new IncidentDetailDto
            {
                IncidentId = incident.IncidentId,
                AreaId = incident.AreaId,
                AreaName = incident.Area.AreaName,
                CategoryId = incident.CategoryId,
                CategoryName = incident.Category != null ? incident.Category.CategoryName : null,
                Title = incident.Title,
                Description = incident.Description,
                LocationText = incident.LocationText,
                Latitude = incident.Latitude,
                Longitude = incident.Longitude,
                Priority = incident.Priority,
                Status = incident.Status,
                DueDate = incident.DueDate,
                ResolvedAt = incident.ResolvedAt,
                ClosedAt = incident.ClosedAt,
                MergedIntoIncidentId = incident.MergedIntoIncidentId,
                ReportCount = incident.IncidentReportLinks.Count(link => link.LinkStatus == IncidentLinkStatus.Active),
                SubscriberCount = incident.IncidentSubscriptions.Count(subscription => subscription.IsActive),
                CreatedAt = incident.CreatedAt,
                UpdatedAt = incident.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Không tìm thấy Incident.");

        detail.Reports = await _uow.GetRepository<IncidentReportLink>().Entities
            .AsNoTracking()
            .Where(link => link.IncidentId == incidentId)
            .OrderBy(link => link.LinkStatus == IncidentLinkStatus.Active ? 0 : 1)
            .ThenBy(link => link.LinkedAt)
            .Select(link => new IncidentReportDto
            {
                IncidentReportLinkId = link.IncidentReportLinkId,
                FeedbackId = link.FeedbackId,
                ReporterUserId = link.Feedback.UserId,
                ReporterName = link.Feedback.User.FullName,
                Title = link.Feedback.Title,
                LocationText = link.Feedback.LocationText,
                SubmissionChannel = link.Feedback.SubmissionChannel,
                FeedbackStatus = link.Feedback.Status,
                LinkStatus = link.LinkStatus,
                LinkMethod = link.LinkMethod,
                LinkRole = link.LinkRole,
                ConfidenceScore = link.ConfidenceScore,
                Reason = link.Reason,
                LinkedByUserId = link.LinkedByUserId,
                LinkedByUserName = link.LinkedByUser != null ? link.LinkedByUser.FullName : null,
                LinkedAt = link.LinkedAt,
                UnlinkedByUserId = link.UnlinkedByUserId,
                UnlinkedByUserName = link.UnlinkedByUser != null ? link.UnlinkedByUser.FullName : null,
                UnlinkedAt = link.UnlinkedAt
            })
            .ToListAsync(cancellationToken);

        detail.Subscribers = await _uow.GetRepository<IncidentSubscription>().Entities
            .AsNoTracking()
            .Where(subscription => subscription.IncidentId == incidentId)
            .OrderByDescending(subscription => subscription.IsActive)
            .ThenBy(subscription => subscription.CreatedAt)
            .Select(subscription => new IncidentSubscriberDto
            {
                UserId = subscription.UserId,
                UserName = subscription.User.FullName,
                SourceType = subscription.SourceType,
                SourceFeedbackId = subscription.SourceFeedbackId,
                IsActive = subscription.IsActive,
                CreatedAt = subscription.CreatedAt
            })
            .ToListAsync(cancellationToken);

        detail.Events = await _uow.GetRepository<IncidentEvent>().Entities
            .AsNoTracking()
            .Where(item => item.IncidentId == incidentId)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.IncidentEventId)
            .Select(item => new IncidentEventDto
            {
                IncidentEventId = item.IncidentEventId,
                FeedbackId = item.FeedbackId,
                EventType = item.EventType,
                ActorUserId = item.ActorUserId,
                ActorUserName = item.ActorUser != null ? item.ActorUser.FullName : null,
                PayloadJson = item.PayloadJson,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return detail;
    }

    public async Task<IncidentDetailDto> LinkReportAsync(
        Guid incidentId,
        LinkIncidentReportRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateLinkRequest(request);
        _uow.BeginTransaction();

        try
        {
            await _uow.AcquireTransactionAdvisoryLockAsync(ToAdvisoryLockKey(request.FeedbackId));

            var incident = await _uow.GetRepository<Incident>().Entities
                .FirstOrDefaultAsync(item => item.IncidentId == incidentId, cancellationToken)
                ?? throw new Exception("Không tìm thấy Incident.");

            if (incident.MergedIntoIncidentId.HasValue || string.Equals(incident.Status, "Merged", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Không thể liên kết Report vào Incident đã được merge.");
            }

            var feedback = await _uow.GetRepository<Feedback>().Entities
                .FirstOrDefaultAsync(item => item.FeedbackId == request.FeedbackId, cancellationToken)
                ?? throw new Exception("Không tìm thấy Feedback.");

            var linkRepository = _uow.GetRepository<IncidentReportLink>();
            var activeLink = await linkRepository.Entities
                .FirstOrDefaultAsync(link =>
                    link.FeedbackId == request.FeedbackId &&
                    link.LinkStatus == IncidentLinkStatus.Active,
                    cancellationToken);

            if (activeLink != null)
            {
                if (activeLink.IncidentId == incidentId)
                {
                    _uow.CommitTransaction();
                    return await GetIncidentDetailAsync(incidentId, cancellationToken);
                }

                throw new Exception("Feedback đang thuộc một Incident khác; hãy unlink trước khi liên kết lại.");
            }

            var hasActiveReports = await linkRepository.Entities.AnyAsync(link =>
                link.IncidentId == incidentId &&
                link.LinkStatus == IncidentLinkStatus.Active,
                cancellationToken);
            var now = DateTime.UtcNow;
            var linkId = Guid.NewGuid();

            await linkRepository.AddAsync(new IncidentReportLink
            {
                IncidentReportLinkId = linkId,
                IncidentId = incidentId,
                FeedbackId = feedback.FeedbackId,
                LinkStatus = IncidentLinkStatus.Active,
                LinkMethod = request.LinkMethod.Trim(),
                LinkRole = hasActiveReports ? IncidentLinkRole.Corroborating : IncidentLinkRole.Primary,
                ConfidenceScore = request.ConfidenceScore,
                Reason = NormalizeOptional(request.Reason),
                LinkedByUserId = actorUserId,
                LinkedAt = now
            });

            await EnsureSubscriptionAsync(
                incidentId,
                feedback.UserId,
                feedback.FeedbackId,
                now,
                cancellationToken);

            await _uow.GetRepository<IncidentEvent>().AddAsync(new IncidentEvent
            {
                IncidentId = incidentId,
                FeedbackId = feedback.FeedbackId,
                EventType = IncidentEventType.ReportLinked,
                ActorUserId = actorUserId,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    incidentReportLinkId = linkId,
                    method = request.LinkMethod.Trim(),
                    request.ConfidenceScore,
                    request.Reason
                }),
                CreatedAt = now
            });

            incident.UpdatedAt = now;
            await _uow.SaveAsync();
            _uow.CommitTransaction();
        }
        catch
        {
            _uow.RollBack();
            throw;
        }

        return await GetIncidentDetailAsync(incidentId, cancellationToken);
    }

    public async Task UnlinkReportAsync(
        Guid incidentId,
        Guid feedbackId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        _uow.BeginTransaction();

        try
        {
            await _uow.AcquireTransactionAdvisoryLockAsync(ToAdvisoryLockKey(feedbackId));
            var linkRepository = _uow.GetRepository<IncidentReportLink>();
            var link = await linkRepository.Entities
                .Include(item => item.Feedback)
                .FirstOrDefaultAsync(item =>
                    item.IncidentId == incidentId &&
                    item.FeedbackId == feedbackId &&
                    item.LinkStatus == IncidentLinkStatus.Active,
                    cancellationToken)
                ?? throw new Exception("Không tìm thấy active Report link trong Incident.");

            var now = DateTime.UtcNow;
            link.LinkStatus = IncidentLinkStatus.Unlinked;
            link.UnlinkedByUserId = actorUserId;
            link.UnlinkedAt = now;
            link.UpdatedAt = now;

            var remainingLinks = await linkRepository.Entities
                .Where(item =>
                    item.IncidentId == incidentId &&
                    item.FeedbackId != feedbackId &&
                    item.LinkStatus == IncidentLinkStatus.Active)
                .OrderBy(item => item.LinkedAt)
                .ToListAsync(cancellationToken);

            if (link.LinkRole == IncidentLinkRole.Primary && remainingLinks.Count > 0)
            {
                remainingLinks[0].LinkRole = IncidentLinkRole.Primary;
                remainingLinks[0].UpdatedAt = now;
            }

            var userHasAnotherReport = await linkRepository.Entities.AnyAsync(item =>
                item.IncidentId == incidentId &&
                item.FeedbackId != feedbackId &&
                item.LinkStatus == IncidentLinkStatus.Active &&
                item.Feedback.UserId == link.Feedback.UserId,
                cancellationToken);

            if (!userHasAnotherReport)
            {
                var subscription = await _uow.GetRepository<IncidentSubscription>().Entities
                    .FirstOrDefaultAsync(item =>
                        item.IncidentId == incidentId &&
                        item.UserId == link.Feedback.UserId,
                        cancellationToken);

                if (subscription != null)
                {
                    subscription.IsActive = false;
                    subscription.UpdatedAt = now;
                }
            }

            await _uow.GetRepository<IncidentEvent>().AddAsync(new IncidentEvent
            {
                IncidentId = incidentId,
                FeedbackId = feedbackId,
                EventType = IncidentEventType.ReportUnlinked,
                ActorUserId = actorUserId,
                PayloadJson = JsonSerializer.Serialize(new { reason = "Manually unlinked by management." }),
                CreatedAt = now
            });

            var incident = await _uow.GetRepository<Incident>().Entities
                .FirstAsync(item => item.IncidentId == incidentId, cancellationToken);
            incident.UpdatedAt = now;

            await _uow.SaveAsync();
            _uow.CommitTransaction();
        }
        catch
        {
            _uow.RollBack();
            throw;
        }
    }

    private async Task EnsureSubscriptionAsync(
        Guid incidentId,
        Guid userId,
        Guid sourceFeedbackId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var repository = _uow.GetRepository<IncidentSubscription>();
        var subscription = await repository.Entities.FirstOrDefaultAsync(item =>
            item.IncidentId == incidentId && item.UserId == userId,
            cancellationToken);

        if (subscription == null)
        {
            await repository.AddAsync(new IncidentSubscription
            {
                IncidentSubscriptionId = Guid.NewGuid(),
                IncidentId = incidentId,
                UserId = userId,
                SourceType = IncidentSubscriptionSource.Report,
                SourceFeedbackId = sourceFeedbackId,
                IsActive = true,
                CreatedAt = now
            });
            return;
        }

        subscription.IsActive = true;
        subscription.SourceType = IncidentSubscriptionSource.Report;
        subscription.SourceFeedbackId ??= sourceFeedbackId;
        subscription.UpdatedAt = now;
    }

    private static void ValidateLinkRequest(LinkIncidentReportRequest request)
    {
        if (request.FeedbackId == Guid.Empty)
        {
            throw new Exception("FeedbackId không hợp lệ.");
        }

        if (!IncidentLinkMethod.ManagementAllowed.Contains(request.LinkMethod.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new Exception("LinkMethod chỉ nhận UserSelected hoặc StaffConfirmed.");
        }

        request.LinkMethod = IncidentLinkMethod.ManagementAllowed.First(method =>
            string.Equals(method, request.LinkMethod.Trim(), StringComparison.OrdinalIgnoreCase));
        ValidateConfidence(request.ConfidenceScore);
    }

    private static void ValidateConfidence(decimal? confidenceScore)
    {
        if (confidenceScore is < 0m or > 1m)
        {
            throw new Exception("ConfidenceScore phải nằm trong khoảng 0 đến 1.");
        }
    }

    private static long ToAdvisoryLockKey(Guid feedbackId)
    {
        return BitConverter.ToInt64(feedbackId.ToByteArray(), 0);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string AppendReason(string? currentReason, string addition)
    {
        return string.IsNullOrWhiteSpace(currentReason)
            ? addition
            : $"{currentReason.Trim()} {addition}";
    }
}
