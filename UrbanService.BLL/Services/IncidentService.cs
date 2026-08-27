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
    private sealed record IncidentStatusUpdateResult(
        IncidentDetailDto Detail,
        IReadOnlyCollection<FeedbackStatusHistory> Histories);

    private readonly IUnitOfWork _uow;
    private readonly INotificationService? _notificationService;

    public IncidentService(IUnitOfWork uow, INotificationService? notificationService = null)
    {
        _uow = uow;
        _notificationService = notificationService;
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
            Severity = IncidentSeverity.Medium,
            Status = IncidentStatus.New,
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

    public async Task StageReportInExistingIncidentAsync(
        Feedback feedback,
        Guid incidentId,
        Guid actorUserId,
        DateTime occurredAt,
        CancellationToken cancellationToken = default)
    {
        var incident = await _uow.GetRepository<Incident>().Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IncidentId == incidentId, cancellationToken)
            ?? throw new Exception("Không tìm thấy Incident.");

        if (incident.MergedIntoIncidentId.HasValue ||
            string.Equals(incident.Status, IncidentStatus.Merged, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Không thể gửi Report vào Incident đã được merge.");
        }

        if (incident.AreaId != feedback.AreaId)
        {
            throw new Exception("Report phải thuộc cùng khu vực với Incident.");
        }

        if (incident.CategoryId.HasValue && feedback.CategoryId.HasValue && incident.CategoryId != feedback.CategoryId)
        {
            throw new Exception("Report phải thuộc cùng danh mục với Incident.");
        }

        var linkId = Guid.NewGuid();
        await _uow.GetRepository<IncidentReportLink>().AddAsync(new IncidentReportLink
        {
            IncidentReportLinkId = linkId,
            IncidentId = incidentId,
            FeedbackId = feedback.FeedbackId,
            LinkStatus = IncidentLinkStatus.Active,
            LinkMethod = IncidentLinkMethod.UserSelected,
            LinkRole = IncidentLinkRole.Corroborating,
            Reason = "Citizen submitted an additional report to an existing incident.",
            LinkedByUserId = actorUserId,
            LinkedAt = occurredAt
        });

        await EnsureSubscriptionAsync(incidentId, feedback.UserId, feedback.FeedbackId, occurredAt, cancellationToken);
        await _uow.GetRepository<IncidentEvent>().AddAsync(new IncidentEvent
        {
            IncidentId = incidentId,
            FeedbackId = feedback.FeedbackId,
            EventType = IncidentEventType.ReportLinked,
            ActorUserId = actorUserId,
            PayloadJson = JsonSerializer.Serialize(new
            {
                incidentReportLinkId = linkId,
                method = IncidentLinkMethod.UserSelected,
                role = IncidentLinkRole.Corroborating
            }),
            CreatedAt = occurredAt
        });
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
        string targetIncidentStatus;

        if (parentLink == null)
        {
            targetIncidentId = await StageNewReportIncidentAsync(
                parentFeedback,
                staffUserId,
                DateTime.UtcNow);
            targetIncidentStatus = IncidentStatus.New;
            await _uow.SaveAsync();
        }
        else
        {
            targetIncidentId = parentLink.IncidentId;
            targetIncidentStatus = parentLink.Incident.Status;
        }

        var childLink = activeLinks.FirstOrDefault(link => link.FeedbackId == childFeedback.FeedbackId);
        if (childLink?.IncidentId == targetIncidentId)
        {
            await ProjectFeedbackStatusAsync(
                childFeedback,
                targetIncidentStatus,
                staffUserId,
                "Synchronized with the canonical incident after duplicate confirmation.",
                DateTime.UtcNow);
            await _uow.SaveAsync();
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

        await ProjectFeedbackStatusAsync(
            childFeedback,
            targetIncidentStatus,
            staffUserId,
            "Synchronized with the canonical incident after duplicate confirmation.",
            now);

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
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var status = NormalizeOptional(query.Status)?.ToLower();
        var priority = NormalizeOptional(query.Priority)?.ToLower();
        var severity = NormalizeOptional(query.Severity)?.ToLower();
        var search = NormalizeOptional(query.Search)?.ToLower();

        var incidents = ManagementAccessRules.ApplyIncidentReadScope(
            _uow.GetRepository<Incident>().Entities.AsNoTracking(),
            actor);

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

        if (!string.IsNullOrWhiteSpace(severity))
        {
            incidents = incidents.Where(incident => incident.Severity.ToLower() == severity);
        }

        if (query.AssignedStaffUserId.HasValue)
        {
            incidents = incidents.Where(incident => incident.AssignedStaffUserId == query.AssignedStaffUserId.Value);
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
                Severity = incident.Severity,
                Status = incident.Status,
                MergedIntoIncidentId = incident.MergedIntoIncidentId,
                AssignedStaffUserId = incident.AssignedStaffUserId,
                AssignedStaffName = incident.AssignedStaffUser != null ? incident.AssignedStaffUser.FullName : null,
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
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        var canRead = await ManagementAccessRules.ApplyIncidentReadScope(
                _uow.GetRepository<Incident>().Entities.AsNoTracking(),
                actor)
            .AnyAsync(incident => incident.IncidentId == incidentId, cancellationToken);
        if (!canRead)
        {
            throw new UrbanService.BLL.Common.ForbiddenAccessException(
                "Bạn không có quyền xem sự vụ này.");
        }

        return await GetIncidentDetailCoreAsync(incidentId, cancellationToken);
    }

    private async Task<IncidentDetailDto> GetIncidentDetailCoreAsync(
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
                Severity = incident.Severity,
                Status = incident.Status,
                DueDate = incident.DueDate,
                ResolvedAt = incident.ResolvedAt,
                ClosedAt = incident.ClosedAt,
                MergedIntoIncidentId = incident.MergedIntoIncidentId,
                AssignedStaffUserId = incident.AssignedStaffUserId,
                AssignedStaffName = incident.AssignedStaffUser != null ? incident.AssignedStaffUser.FullName : null,
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
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, incidentId, cancellationToken);
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
            ManagementAccessRules.EnsureManagerArea(actor, feedback.AreaId);

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
                    return await GetIncidentDetailCoreAsync(incidentId, cancellationToken);
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

        return await GetIncidentDetailCoreAsync(incidentId, cancellationToken);
    }

    public async Task UnlinkReportAsync(
        Guid incidentId,
        Guid feedbackId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, incidentId, cancellationToken);
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

    public async Task<PagedResultDto<PublicIncidentListItemDto>> GetPublicIncidentsAsync(
        IncidentQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var status = NormalizeOptional(query.Status)?.ToLower();
        var priority = NormalizeOptional(query.Priority)?.ToLower();
        var severity = NormalizeOptional(query.Severity)?.ToLower();
        var search = NormalizeOptional(query.Search)?.ToLower();

        var incidents = _uow.GetRepository<Incident>().Entities
            .AsNoTracking()
            .Where(incident =>
                incident.MergedIntoIncidentId == null &&
                incident.IncidentReportLinks.Any(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Feedback.Status != FeedbackStatus.Submitted &&
                    link.Feedback.Status != FeedbackStatus.AiReviewed));

        if (query.AreaId.HasValue)
            incidents = incidents.Where(item => item.AreaId == query.AreaId.Value);
        if (query.CategoryId.HasValue)
            incidents = incidents.Where(item => item.CategoryId == query.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            incidents = incidents.Where(item => item.Status.ToLower() == status);
        if (!string.IsNullOrWhiteSpace(priority))
            incidents = incidents.Where(item => item.Priority != null && item.Priority.ToLower() == priority);
        if (!string.IsNullOrWhiteSpace(severity))
            incidents = incidents.Where(item => item.Severity.ToLower() == severity);
        if (!string.IsNullOrWhiteSpace(search))
        {
            incidents = incidents.Where(item =>
                item.Title.ToLower().Contains(search) ||
                (item.Description != null && item.Description.ToLower().Contains(search)) ||
                item.LocationText.ToLower().Contains(search));
        }

        var totalItems = await incidents.CountAsync(cancellationToken);
        var items = await incidents
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PublicIncidentListItemDto
            {
                IncidentId = item.IncidentId,
                AreaId = item.AreaId,
                AreaName = item.Area.AreaName,
                CategoryId = item.CategoryId,
                CategoryName = item.Category != null ? item.Category.CategoryName : null,
                Title = item.Title,
                Description = item.Description,
                LocationText = item.LocationText,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Priority = item.Priority,
                Severity = item.Severity,
                Status = item.Status,
                ReportCount = item.IncidentReportLinks.Count(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Feedback.Status != FeedbackStatus.Submitted &&
                    link.Feedback.Status != FeedbackStatus.AiReviewed),
                SubscriberCount = item.IncidentSubscriptions.Count(subscription => subscription.IsActive),
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<PublicIncidentListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<PublicIncidentDetailDto> GetPublicIncidentDetailAsync(
        Guid incidentId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var detail = await _uow.GetRepository<Incident>().Entities
            .AsNoTracking()
            .Where(item =>
                item.IncidentId == incidentId &&
                item.MergedIntoIncidentId == null &&
                item.IncidentReportLinks.Any(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Feedback.Status != FeedbackStatus.Submitted &&
                    link.Feedback.Status != FeedbackStatus.AiReviewed))
            .Select(item => new PublicIncidentDetailDto
            {
                IncidentId = item.IncidentId,
                AreaId = item.AreaId,
                AreaName = item.Area.AreaName,
                CategoryId = item.CategoryId,
                CategoryName = item.Category != null ? item.Category.CategoryName : null,
                Title = item.Title,
                Description = item.Description,
                LocationText = item.LocationText,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Priority = item.Priority,
                Severity = item.Severity,
                Status = item.Status,
                ReportCount = item.IncidentReportLinks.Count(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Feedback.Status != FeedbackStatus.Submitted &&
                    link.Feedback.Status != FeedbackStatus.AiReviewed),
                SubscriberCount = item.IncidentSubscriptions.Count(subscription => subscription.IsActive),
                DueDate = item.DueDate,
                ResolvedAt = item.ResolvedAt,
                ClosedAt = item.ClosedAt,
                IsSubscribedByCurrentUser = currentUserId != Guid.Empty && item.IncidentSubscriptions.Any(subscription =>
                    subscription.UserId == currentUserId && subscription.IsActive),
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return detail ?? throw new Exception("Không tìm thấy Incident công khai.");
    }

    public async Task<IReadOnlyCollection<PublicIncidentReportDto>> GetPublicIncidentReportsAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePublicIncidentExistsAsync(incidentId, cancellationToken);

        return await _uow.GetRepository<IncidentReportLink>().Entities
            .AsNoTracking()
            .Where(link =>
                link.IncidentId == incidentId &&
                link.LinkStatus == IncidentLinkStatus.Active &&
                link.Feedback.Status != FeedbackStatus.Submitted &&
                link.Feedback.Status != FeedbackStatus.AiReviewed)
            .OrderBy(link => link.LinkRole == IncidentLinkRole.Primary ? 0 : 1)
            .ThenBy(link => link.LinkedAt)
            .Select(link => new PublicIncidentReportDto
            {
                FeedbackId = link.FeedbackId,
                Title = link.Feedback.Title,
                Description = link.Feedback.Description,
                LocationText = link.Feedback.LocationText,
                SubmissionChannel = link.Feedback.SubmissionChannel,
                Status = link.Feedback.Status,
                CreatedAt = link.Feedback.CreatedAt,
                Attachments = link.Feedback.FeedbackAttachments
                    .OrderBy(attachment => attachment.UploadedAt)
                    .Select(attachment => new FeedbackAttachmentDto
                    {
                        AttachmentId = attachment.AttachmentId,
                        FileUrl = attachment.FileUrl,
                        FileType = attachment.FileType,
                        UploadedAt = attachment.UploadedAt
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResultDto<PublicIncidentEventDto>> GetPublicTimelineAsync(
        Guid incidentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsurePublicIncidentExistsAsync(incidentId, cancellationToken);
        pageNumber = Math.Max(1, pageNumber);
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);
        var query = _uow.GetRepository<IncidentEvent>().Entities.AsNoTracking()
            .Where(item => item.IncidentId == incidentId);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.IncidentEventId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PublicIncidentEventDto
            {
                IncidentEventId = item.IncidentEventId,
                EventType = item.EventType,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<PublicIncidentEventDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<PagedResultDto<IncidentEventDto>> GetManagementTimelineAsync(
        Guid incidentId,
        int pageNumber,
        int pageSize,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        var canRead = await ManagementAccessRules.ApplyIncidentReadScope(
                _uow.GetRepository<Incident>().Entities.AsNoTracking(),
                actor)
            .AnyAsync(incident => incident.IncidentId == incidentId, cancellationToken);
        if (!canRead)
        {
            throw new UrbanService.BLL.Common.ForbiddenAccessException(
                "Bạn không có quyền xem dòng thời gian của sự vụ này.");
        }
        pageNumber = Math.Max(1, pageNumber);
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);
        var query = _uow.GetRepository<IncidentEvent>().Entities.AsNoTracking()
            .Where(item => item.IncidentId == incidentId);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.IncidentEventId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
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

        return new PagedResultDto<IncidentEventDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<PagedResultDto<IncidentListItemDto>> GetMyIncidentsAsync(
        Guid userId,
        IncidentQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var incidents = _uow.GetRepository<Incident>().Entities.AsNoTracking()
            .Where(item =>
                item.MergedIntoIncidentId == null &&
                item.IncidentSubscriptions.Any(subscription => subscription.UserId == userId && subscription.IsActive));

        if (query.AreaId.HasValue)
            incidents = incidents.Where(item => item.AreaId == query.AreaId.Value);
        if (query.CategoryId.HasValue)
            incidents = incidents.Where(item => item.CategoryId == query.CategoryId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLower();
            incidents = incidents.Where(item => item.Status.ToLower() == status);
        }

        var totalItems = await incidents.CountAsync(cancellationToken);
        var items = await incidents
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new IncidentListItemDto
            {
                IncidentId = item.IncidentId,
                AreaId = item.AreaId,
                AreaName = item.Area.AreaName,
                CategoryId = item.CategoryId,
                CategoryName = item.Category != null ? item.Category.CategoryName : null,
                Title = item.Title,
                LocationText = item.LocationText,
                Latitude = item.Latitude,
                Longitude = item.Longitude,
                Priority = item.Priority,
                Severity = item.Severity,
                Status = item.Status,
                MergedIntoIncidentId = item.MergedIntoIncidentId,
                AssignedStaffUserId = item.AssignedStaffUserId,
                AssignedStaffName = item.AssignedStaffUser != null ? item.AssignedStaffUser.FullName : null,
                ReportCount = item.IncidentReportLinks.Count(link => link.LinkStatus == IncidentLinkStatus.Active),
                SubscriberCount = item.IncidentSubscriptions.Count(subscription => subscription.IsActive),
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
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

    public async Task SubscribeAsync(Guid incidentId, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsurePublicIncidentExistsAsync(incidentId, cancellationToken);
        var now = DateTime.UtcNow;
        var repository = _uow.GetRepository<IncidentSubscription>();
        var subscription = await repository.Entities.FirstOrDefaultAsync(item =>
            item.IncidentId == incidentId && item.UserId == userId, cancellationToken);

        if (subscription == null)
        {
            await repository.AddAsync(new IncidentSubscription
            {
                IncidentSubscriptionId = Guid.NewGuid(),
                IncidentId = incidentId,
                UserId = userId,
                SourceType = IncidentSubscriptionSource.Manual,
                IsActive = true,
                CreatedAt = now
            });
        }
        else
        {
            subscription.IsActive = true;
            subscription.SourceType = IncidentSubscriptionSource.Manual;
            subscription.UpdatedAt = now;
        }

        await _uow.SaveAsync();
    }

    public async Task UnsubscribeAsync(Guid incidentId, Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _uow.GetRepository<IncidentSubscription>().Entities
            .FirstOrDefaultAsync(item => item.IncidentId == incidentId && item.UserId == userId, cancellationToken)
            ?? throw new Exception("Bạn chưa theo dõi Incident này.");
        subscription.IsActive = false;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
    }

    public async Task<IncidentDetailDto> UpdateIncidentAsync(
        Guid incidentId,
        UpdateIncidentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, incidentId, cancellationToken);
        var incident = await GetMutableIncidentAsync(incidentId, cancellationToken);
        if (request.AreaId.HasValue)
        {
            ManagementAccessRules.EnsureManagerArea(actor, request.AreaId.Value);
            var areaExists = await _uow.GetRepository<OperatingArea>().Entities.AsNoTracking()
                .AnyAsync(item => item.AreaId == request.AreaId.Value && item.IsActive, cancellationToken);
            if (!areaExists) throw new Exception("Khu vực không tồn tại hoặc đã bị khóa.");
            incident.AreaId = request.AreaId.Value;
        }

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _uow.GetRepository<UrbanServiceCategory>().Entities.AsNoTracking()
                .AnyAsync(item => item.CategoryId == request.CategoryId.Value && item.IsActive, cancellationToken);
            if (!categoryExists) throw new Exception("Danh mục không tồn tại hoặc đã bị khóa.");
            incident.CategoryId = request.CategoryId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Title)) incident.Title = request.Title.Trim();
        if (request.Description != null) incident.Description = NormalizeOptional(request.Description);
        if (!string.IsNullOrWhiteSpace(request.LocationText)) incident.LocationText = request.LocationText.Trim();
        if (request.Latitude.HasValue) incident.Latitude = request.Latitude;
        if (request.Longitude.HasValue) incident.Longitude = request.Longitude;
        if (request.Priority != null) incident.Priority = NormalizeOptional(request.Priority);
        if (request.Severity != null) incident.Severity = NormalizeSeverity(request.Severity);
        if (request.DueDate.HasValue) incident.DueDate = request.DueDate;
        incident.UpdatedAt = DateTime.UtcNow;

        await AddIncidentEventAsync(incidentId, IncidentEventType.IncidentUpdated, actorUserId, new
        {
            incident.AreaId,
            incident.CategoryId,
            incident.Priority,
            incident.Severity
        });
        await _uow.SaveAsync();
        return await GetIncidentDetailCoreAsync(incidentId, cancellationToken);
    }

    public async Task<IncidentDetailDto> UpdateStatusAsync(
        Guid incidentId,
        UpdateIncidentStatusRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, incidentId, cancellationToken);
        var requestedStatus = NormalizeIncidentStatus(request.Status);
        if (requestedStatus != IncidentStatus.Rejected &&
            requestedStatus != IncidentStatus.Cancelled)
        {
            throw new Exception(
                "Endpoint trạng thái chung chỉ dùng để từ chối hoặc hủy sự vụ. " +
                "Xác nhận phản ánh phải đi qua endpoint verify để kiểm tra trùng và khởi tạo SLA. " +
                "Các trạng thái xử lý phải đi qua luồng phân công, bên thứ ba và phê duyệt.");
        }

        var result = await UpdateStatusCoreAsync(
            incidentId,
            request,
            actorUserId,
            cancellationToken);
        return result.Detail;
    }

    public async Task<FeedbackStatusHistoryDto> UpdateStatusFromFeedbackAsync(
        Guid feedbackId,
        UpdateIncidentStatusRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var incidentId = await _uow.GetRepository<IncidentReportLink>().Entities
            .AsNoTracking()
            .Where(link =>
                link.FeedbackId == feedbackId &&
                link.LinkStatus == IncidentLinkStatus.Active)
            .Select(link => (Guid?)link.IncidentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Feedback không có Incident đang hoạt động.");

        var result = await UpdateStatusCoreAsync(
            incidentId,
            request,
            actorUserId,
            cancellationToken);
        var history = result.Histories.SingleOrDefault(item => item.FeedbackId == feedbackId)
            ?? throw new Exception("Trạng thái Feedback đã đồng bộ với Incident.");

        return MapFeedbackStatusHistory(history);
    }

    private async Task<IncidentStatusUpdateResult> UpdateStatusCoreAsync(
        Guid incidentId,
        UpdateIncidentStatusRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var status = NormalizeIncidentStatus(request.Status);
        var histories = new List<FeedbackStatusHistory>();
        Incident incident;
        var statusChanged = false;
        _uow.BeginTransaction();
        try
        {
            await _uow.AcquireTransactionAdvisoryLockAsync(ToAdvisoryLockKey(incidentId));
            incident = await GetMutableIncidentAsync(incidentId, cancellationToken);
            if (string.Equals(status, IncidentStatus.New, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(incident.Status, IncidentStatus.New, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Không thể đưa Incident quay lại trạng thái New.");
            }

            if (string.Equals(incident.Status, status, StringComparison.OrdinalIgnoreCase))
            {
                _uow.CommitTransaction();
            }
            else
            {
                statusChanged = true;
                var now = DateTime.UtcNow;
                var oldStatus = incident.Status;
                var note = NormalizeOptional(request.Note);
                var feedbackStatus = MapIncidentStatusToFeedbackStatus(status);
                if (feedbackStatus != null)
                {
                    var activeLinks = await _uow.GetRepository<IncidentReportLink>().Entities
                        .Include(link => link.Feedback)
                        .Where(link =>
                            link.IncidentId == incidentId &&
                            link.LinkStatus == IncidentLinkStatus.Active)
                        .ToListAsync(cancellationToken);

                    foreach (var feedback in activeLinks
                        .Select(link => link.Feedback)
                        .DistinctBy(feedback => feedback.FeedbackId))
                    {
                        if (string.Equals(feedback.Status, feedbackStatus, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        histories.Add(new FeedbackStatusHistory
                        {
                            FeedbackId = feedback.FeedbackId,
                            ChangedByUserId = actorUserId,
                            OldStatus = feedback.Status,
                            NewStatus = feedbackStatus,
                            Note = note,
                            ChangedAt = now
                        });
                        feedback.Status = feedbackStatus;
                        feedback.UpdatedAt = now;
                    }
                }

                if (histories.Count > 0)
                {
                    await _uow.GetRepository<FeedbackStatusHistory>().AddRangeAsync(histories);
                }

                incident.Status = status;
                incident.UpdatedAt = now;
                incident.ResolvedAt = status == IncidentStatus.Resolved ? now : incident.ResolvedAt;
                incident.ClosedAt = status == IncidentStatus.Closed ? now : incident.ClosedAt;

                await AddIncidentEventAsync(incidentId, IncidentEventType.StatusChanged, actorUserId, new
                {
                    oldStatus,
                    newStatus = status,
                    note
                });
                await _uow.SaveAsync();
                _uow.CommitTransaction();
            }
        }
        catch
        {
            _uow.RollBack();
            throw;
        }

        if (statusChanged)
        {
            await NotifyIncidentSubscribersAsync(
                incidentId,
                "Sự vụ đã cập nhật trạng thái",
                $"Sự vụ \"{incident.Title}\" đã chuyển sang trạng thái {status}.",
                cancellationToken);
        }
        return new IncidentStatusUpdateResult(
            await GetIncidentDetailCoreAsync(incidentId, cancellationToken),
            histories);
    }

    public async Task<IReadOnlyCollection<IncidentAssigneeCandidateDto>> GetAssigneeCandidatesAsync(
        Guid incidentId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, incidentId, cancellationToken);
        var incident = await _uow.GetRepository<Incident>().Entities.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IncidentId == incidentId, cancellationToken)
            ?? throw new Exception("Không tìm thấy Incident.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _uow.GetRepository<StaffAreaAssignment>().Entities.AsNoTracking()
            .Where(assignment =>
                assignment.IsActive &&
                assignment.User.IsActive &&
                assignment.User.Role.RoleName == UserRole.SYSTEMSTAFF &&
                assignment.AreaId == incident.AreaId &&
                (!assignment.CategoryId.HasValue || assignment.CategoryId == incident.CategoryId) &&
                (!assignment.StartDate.HasValue || assignment.StartDate <= today) &&
                (!assignment.EndDate.HasValue || assignment.EndDate >= today))
            .OrderByDescending(assignment => assignment.CategoryId.HasValue)
            .ThenByDescending(assignment => assignment.IsPrimary)
            .ThenBy(assignment => assignment.User.FullName)
            .Select(assignment => new IncidentAssigneeCandidateDto
            {
                UserId = assignment.UserId,
                StaffName = assignment.User.FullName,
                Email = assignment.User.Email,
                AreaId = assignment.AreaId,
                AreaName = assignment.Area.AreaName,
                CategoryId = assignment.CategoryId,
                CategoryName = assignment.Category != null ? assignment.Category.CategoryName : null,
                IsPrimary = assignment.IsPrimary
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IncidentDetailDto> AssignAsync(
        Guid incidentId,
        AssignIncidentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, incidentId, cancellationToken);
        if (request.StaffUserId == Guid.Empty) throw new Exception("StaffUserId không hợp lệ.");
        var incident = await GetMutableIncidentAsync(incidentId, cancellationToken);
        if (incident.Status != IncidentStatus.Verified &&
            incident.Status != IncidentStatus.Assigned &&
            incident.Status != IncidentStatus.InProgress &&
            incident.Status != IncidentStatus.NeedRework)
        {
            throw new Exception("Incident phải được xác nhận trước khi phân công Staff.");
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var eligible = await _uow.GetRepository<StaffAreaAssignment>().Entities.AsNoTracking()
            .AnyAsync(assignment =>
                assignment.UserId == request.StaffUserId &&
                assignment.IsActive &&
                assignment.User.IsActive &&
                assignment.User.Role.RoleName == UserRole.SYSTEMSTAFF &&
                assignment.AreaId == incident.AreaId &&
                (!assignment.CategoryId.HasValue || assignment.CategoryId == incident.CategoryId) &&
                (!assignment.StartDate.HasValue || assignment.StartDate <= today) &&
                (!assignment.EndDate.HasValue || assignment.EndDate >= today),
                cancellationToken);

        if (!eligible)
            throw new Exception("Staff không phụ trách khu vực và danh mục của Incident.");

        var oldAssignee = incident.AssignedStaffUserId;
        incident.AssignedStaffUserId = request.StaffUserId;
        var now = DateTime.UtcNow;
        incident.UpdatedAt = now;
        if (string.Equals(incident.Status, IncidentStatus.Verified, StringComparison.OrdinalIgnoreCase))
        {
            var oldStatus = incident.Status;
            incident.Status = IncidentStatus.Assigned;
            var activeLinks = await _uow.GetRepository<IncidentReportLink>().Entities
                .Include(link => link.Feedback)
                .Where(link =>
                    link.IncidentId == incidentId &&
                    link.LinkStatus == IncidentLinkStatus.Active)
                .ToListAsync(cancellationToken);
            foreach (var feedback in activeLinks
                .Select(link => link.Feedback)
                .DistinctBy(feedback => feedback.FeedbackId))
            {
                await ProjectFeedbackStatusAsync(
                    feedback,
                    IncidentStatus.Assigned,
                    actorUserId,
                    "Manager đã phân công Staff phụ trách sự vụ.",
                    now);
            }

            await AddIncidentEventAsync(incidentId, IncidentEventType.StatusChanged, actorUserId, new
            {
                oldStatus,
                newStatus = IncidentStatus.Assigned,
                note = "Manager assigned staff"
            });
        }

        await AddIncidentEventAsync(incidentId, IncidentEventType.AssigneeChanged, actorUserId, new
        {
            oldAssignedStaffUserId = oldAssignee,
            assignedStaffUserId = request.StaffUserId,
            reason = NormalizeOptional(request.Reason)
        });
        await _uow.SaveAsync();

        if (_notificationService != null)
        {
            await _notificationService.SendAsync(
                request.StaffUserId,
                "Bạn được phân công xử lý sự vụ",
                $"Bạn được phân công xử lý sự vụ \"{incident.Title}\".",
                NotificationType.TicketUpdated,
                $"/management/incidents/{incidentId}",
                incidentId,
                "Incident",
                incidentId.ToString());
        }

        return await GetIncidentDetailCoreAsync(incidentId, cancellationToken);
    }

    public async Task<IncidentDetailDto> MergeAsync(
        Guid sourceIncidentId,
        MergeIncidentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.TargetIncidentId == Guid.Empty || request.TargetIncidentId == sourceIncidentId)
            throw new Exception("TargetIncidentId không hợp lệ.");

        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, sourceIncidentId, cancellationToken);
        await EnsureManagerIncidentAccessAsync(actor, request.TargetIncidentId, cancellationToken);

        _uow.BeginTransaction();
        try
        {
            var incidents = await _uow.GetRepository<Incident>().Entities
                .Where(item => item.IncidentId == sourceIncidentId || item.IncidentId == request.TargetIncidentId)
                .ToListAsync(cancellationToken);
            var source = incidents.FirstOrDefault(item => item.IncidentId == sourceIncidentId)
                ?? throw new Exception("Không tìm thấy Incident nguồn.");
            var target = incidents.FirstOrDefault(item => item.IncidentId == request.TargetIncidentId)
                ?? throw new Exception("Không tìm thấy Incident đích.");
            if (source.MergedIntoIncidentId.HasValue || target.MergedIntoIncidentId.HasValue)
                throw new Exception("Không thể merge Incident đã được merge.");
            if (source.AreaId != target.AreaId)
                throw new Exception("Chỉ có thể merge các Incident cùng khu vực.");

            var links = await _uow.GetRepository<IncidentReportLink>().Entities
                .Where(link =>
                    (link.IncidentId == sourceIncidentId || link.IncidentId == request.TargetIncidentId) &&
                    link.LinkStatus == IncidentLinkStatus.Active)
                .ToListAsync(cancellationToken);
            var targetFeedbackIds = links.Where(link => link.IncidentId == request.TargetIncidentId)
                .Select(link => link.FeedbackId).ToHashSet();
            var now = DateTime.UtcNow;

            foreach (var sourceLink in links.Where(link => link.IncidentId == sourceIncidentId))
            {
                sourceLink.LinkStatus = IncidentLinkStatus.Unlinked;
                sourceLink.UnlinkedByUserId = actorUserId;
                sourceLink.UnlinkedAt = now;
                sourceLink.UpdatedAt = now;
                if (!targetFeedbackIds.Add(sourceLink.FeedbackId)) continue;

                await _uow.GetRepository<IncidentReportLink>().AddAsync(new IncidentReportLink
                {
                    IncidentReportLinkId = Guid.NewGuid(),
                    IncidentId = request.TargetIncidentId,
                    FeedbackId = sourceLink.FeedbackId,
                    LinkStatus = IncidentLinkStatus.Active,
                    LinkMethod = IncidentLinkMethod.StaffConfirmed,
                    LinkRole = IncidentLinkRole.Corroborating,
                    Reason = NormalizeOptional(request.Reason) ?? "Incident merged by management.",
                    LinkedByUserId = actorUserId,
                    LinkedAt = now
                });
            }

            var sourceSubscriptions = await _uow.GetRepository<IncidentSubscription>().Entities
                .Where(item => item.IncidentId == sourceIncidentId && item.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var subscription in sourceSubscriptions)
            {
                subscription.IsActive = false;
                subscription.UpdatedAt = now;
                var targetSubscription = await _uow.GetRepository<IncidentSubscription>().Entities
                    .FirstOrDefaultAsync(item =>
                        item.IncidentId == request.TargetIncidentId && item.UserId == subscription.UserId,
                        cancellationToken);
                if (targetSubscription == null)
                {
                    await _uow.GetRepository<IncidentSubscription>().AddAsync(new IncidentSubscription
                    {
                        IncidentSubscriptionId = Guid.NewGuid(),
                        IncidentId = request.TargetIncidentId,
                        UserId = subscription.UserId,
                        SourceType = subscription.SourceType,
                        SourceFeedbackId = subscription.SourceFeedbackId,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
                else
                {
                    targetSubscription.IsActive = true;
                    targetSubscription.UpdatedAt = now;
                }
            }

            source.Status = IncidentStatus.Merged;
            source.MergedIntoIncidentId = request.TargetIncidentId;
            source.UpdatedAt = now;
            target.UpdatedAt = now;
            await AddIncidentEventAsync(sourceIncidentId, IncidentEventType.IncidentMerged, actorUserId, new
            {
                mergedIntoIncidentId = request.TargetIncidentId,
                reason = NormalizeOptional(request.Reason)
            });
            await AddIncidentEventAsync(request.TargetIncidentId, IncidentEventType.IncidentMerged, actorUserId, new
            {
                mergedFromIncidentId = sourceIncidentId,
                reason = NormalizeOptional(request.Reason)
            });
            await _uow.SaveAsync();
            _uow.CommitTransaction();
        }
        catch
        {
            _uow.RollBack();
            throw;
        }

        await NotifyIncidentSubscribersAsync(
            request.TargetIncidentId,
            "Sự vụ đã được hợp nhất",
            "Sự vụ bạn theo dõi đã được hợp nhất với một sự vụ liên quan.",
            cancellationToken);
        return await GetIncidentDetailCoreAsync(request.TargetIncidentId, cancellationToken);
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

    private async Task<Incident> GetMutableIncidentAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _uow.GetRepository<Incident>().Entities
            .FirstOrDefaultAsync(item => item.IncidentId == incidentId, cancellationToken)
            ?? throw new Exception("Không tìm thấy Incident.");
        if (incident.MergedIntoIncidentId.HasValue ||
            string.Equals(incident.Status, IncidentStatus.Merged, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Không thể cập nhật Incident đã được merge.");
        }

        return incident;
    }

    private async Task EnsureManagerIncidentAccessAsync(
        ManagementActorScope actor,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        ManagementAccessRules.EnsureManagerRole(actor);
        var areaId = await _uow.GetRepository<Incident>().Entities
            .AsNoTracking()
            .Where(incident => incident.IncidentId == incidentId)
            .Select(incident => (int?)incident.AreaId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Không tìm thấy Incident.");
        ManagementAccessRules.EnsureManagerArea(actor, areaId);
    }

    private async Task EnsureIncidentExistsAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var exists = await _uow.GetRepository<Incident>().Entities.AsNoTracking()
            .AnyAsync(item => item.IncidentId == incidentId, cancellationToken);
        if (!exists) throw new Exception("Không tìm thấy Incident.");
    }

    private async Task EnsurePublicIncidentExistsAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var exists = await _uow.GetRepository<Incident>().Entities.AsNoTracking()
            .AnyAsync(item =>
                item.IncidentId == incidentId &&
                item.MergedIntoIncidentId == null &&
                item.IncidentReportLinks.Any(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Feedback.Status != FeedbackStatus.Submitted &&
                    link.Feedback.Status != FeedbackStatus.AiReviewed),
                cancellationToken);
        if (!exists) throw new Exception("Không tìm thấy Incident công khai.");
    }

    private async Task AddIncidentEventAsync(
        Guid incidentId,
        string eventType,
        Guid actorUserId,
        object payload)
    {
        await _uow.GetRepository<IncidentEvent>().AddAsync(new IncidentEvent
        {
            IncidentId = incidentId,
            EventType = eventType,
            ActorUserId = actorUserId,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task NotifyIncidentSubscribersAsync(
        Guid incidentId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        if (_notificationService == null) return;
        var userIds = await _uow.GetRepository<IncidentSubscription>().Entities.AsNoTracking()
            .Where(item => item.IncidentId == incidentId && item.IsActive)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            await _notificationService.SendAsync(
                userId,
                title,
                message,
                NotificationType.TicketUpdated,
                $"/community/incidents/{incidentId}",
                incidentId,
                "Incident",
                incidentId.ToString());
        }
    }

    private static string NormalizeSeverity(string severity)
    {
        var normalized = IncidentSeverity.All.FirstOrDefault(item =>
            string.Equals(item, severity.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalized ?? throw new Exception("Severity chỉ nhận Low, Medium, High hoặc Critical.");
    }

    private static string NormalizeIncidentStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) throw new Exception("Status là bắt buộc.");
        var normalized = IncidentStatus.ManagementAllowed.FirstOrDefault(item =>
            string.Equals(item, status.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalized ?? throw new Exception("Status Incident không hợp lệ.");
    }

    private static string? MapIncidentStatusToFeedbackStatus(string status)
    {
        return status switch
        {
            IncidentStatus.Verified => FeedbackStatus.Verified,
            IncidentStatus.Assigned => FeedbackStatus.Assigned,
            IncidentStatus.InProgress => FeedbackStatus.InProgress,
            IncidentStatus.Resolved => FeedbackStatus.Resolved,
            IncidentStatus.SubmittedForApproval => FeedbackStatus.SubmittedForApproval,
            IncidentStatus.Approved => FeedbackStatus.Approved,
            IncidentStatus.Rejected => FeedbackStatus.Rejected,
            IncidentStatus.NeedRework => FeedbackStatus.NeedRework,
            IncidentStatus.Closed => FeedbackStatus.Closed,
            IncidentStatus.Cancelled => FeedbackStatus.Cancelled,
            _ => null
        };
    }

    private async Task ProjectFeedbackStatusAsync(
        Feedback feedback,
        string incidentStatus,
        Guid actorUserId,
        string note,
        DateTime changedAt)
    {
        var projectedStatus = MapIncidentStatusToFeedbackStatus(incidentStatus);
        if (projectedStatus == null ||
            string.Equals(feedback.Status, projectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _uow.GetRepository<FeedbackStatusHistory>().AddAsync(new FeedbackStatusHistory
        {
            FeedbackId = feedback.FeedbackId,
            ChangedByUserId = actorUserId,
            OldStatus = feedback.Status,
            NewStatus = projectedStatus,
            Note = note,
            ChangedAt = changedAt
        });
        feedback.Status = projectedStatus;
        feedback.UpdatedAt = changedAt;
    }

    private static FeedbackStatusHistoryDto MapFeedbackStatusHistory(FeedbackStatusHistory history)
    {
        return new FeedbackStatusHistoryDto
        {
            HistoryId = history.HistoryId,
            FeedbackId = history.FeedbackId,
            ChangedByUserId = history.ChangedByUserId,
            OldStatus = history.OldStatus,
            NewStatus = history.NewStatus,
            Note = history.Note,
            ChangedAt = history.ChangedAt
        };
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
