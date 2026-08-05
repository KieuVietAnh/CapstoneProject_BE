using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.SLA;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class SlaService : ISlaService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SlaService> _logger;
    private readonly SlaMonitoringOptions _slaOptions;
    private readonly IEmailSender _emailSender;

    public SlaService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    IEmailSender emailSender,
    ILogger<SlaService> logger,
    IOptions<SlaMonitoringOptions> slaOptions)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _logger = logger;
        _slaOptions = slaOptions.Value;
    }


    public async Task<List<SlaTimelineDto>> GetTimelineAsync(
    Guid feedbackId)
    {
        var sla = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.FeedbackId == feedbackId &&
                x.IsCurrent);


        if (sla == null)
        {
            throw new KeyNotFoundException(
                "Feedback chưa có SLA.");
        }



        return await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.FeedbackSlaId ==
                sla.FeedbackSlaId)
            .OrderByDescending(x =>
                x.CreatedAt)
            .Select(x =>
                new SlaTimelineDto
                {
                    SlaEventId =
                        x.SlaEventId,

                    EventType =
                        x.EventType,

                    OldStatus =
                        x.OldStatus,

                    NewStatus =
                        x.NewStatus,

                    Note =
                        x.Note,

                    TriggerSource =
                        x.TriggerSource,

                    CreatedAt =
                        x.CreatedAt
                })
            .ToListAsync();
    }

    public async Task<SlaStatusDto> GetStatusAsync(
    Guid feedbackId)
    {
        var sla = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.FeedbackId == feedbackId &&
                x.IsCurrent);


        if (sla == null)
        {
            throw new KeyNotFoundException(
                "Feedback chưa có SLA.");
        }


        var now = DateTime.UtcNow;


        var responseTotal =
            (sla.ResponseDueAt - sla.StartedAt)
            .TotalMinutes;


        var resolutionTotal =
            (sla.ResolutionDueAt - sla.StartedAt)
            .TotalMinutes;



        var responseUsed =
    (now - sla.StartedAt)
    .TotalMinutes
    - sla.TotalPausedMinutes;


        var resolutionUsed =
            (now - sla.StartedAt)
            .TotalMinutes
            - sla.TotalPausedMinutes;



        return new SlaStatusDto
        {
            FeedbackId = sla.FeedbackId,

            FeedbackSlaId =
                sla.FeedbackSlaId,


            Status =
                sla.Status,


            ResponseStatus =
                sla.ResponseStatus,


            ResolutionStatus =
                sla.ResolutionStatus,


            StartedAt =
                sla.StartedAt,


            ResponseDueAt =
                sla.ResponseDueAt,


            ResolutionDueAt =
                sla.ResolutionDueAt,



            ResponseRemainingMinutes =
                Math.Max(
                    0,
                    (int)
                    (sla.ResponseDueAt - now)
                    .TotalMinutes),



            ResolutionRemainingMinutes =
                Math.Max(
                    0,
                    (int)
                    (sla.ResolutionDueAt - now)
                    .TotalMinutes),



            ResponseProgressPercent =
                CalculatePercent(
                    responseUsed,
                    responseTotal),



            ResolutionProgressPercent =
                CalculatePercent(
                    resolutionUsed,
                    resolutionTotal),



            IsResponseWarning =
                sla.ResponseStatus == "Warning",


            IsResolutionWarning =
                sla.ResolutionStatus == "Warning",


            IsResponseBreached =
                sla.IsResponseBreached,


            IsResolutionBreached =
                sla.IsResolutionBreached
        };
    }

    public async Task<FeedbackSlaDto> StartAsync(
        Guid feedbackId,
        Guid startedByUserId)
    {
        ValidateFeedbackId(feedbackId);
        ValidateUserId(startedByUserId);

        await EnsureUserExistsAsync(startedByUserId);

        var feedback = await _unitOfWork
            .GetRepository<Feedback>()
            .Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.FeedbackId == feedbackId);

        if (feedback == null)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy feedback.");
        }

        if (!string.Equals(
                feedback.Status,
                FeedbackStatus.Verified,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Chỉ được bắt đầu SLA sau khi feedback đã được xác minh.");
        }

        if (feedback.AreaId <= 0)
        {
            throw new InvalidOperationException(
                "Feedback chưa xác định khu vực nên chưa thể bắt đầu SLA.");
        }

        if (!feedback.CategoryId.HasValue)
        {
            throw new InvalidOperationException(
                "Feedback chưa có category nên chưa thể bắt đầu SLA.");
        }

        if (string.IsNullOrWhiteSpace(feedback.Priority))
        {
            throw new InvalidOperationException(
                "Feedback chưa có priority nên chưa thể bắt đầu SLA.");
        }

        var existingCurrentSla = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x =>
                x.FeedbackId == feedbackId &&
                x.IsCurrent);

        if (existingCurrentSla)
        {
            throw new InvalidOperationException(
                "Feedback đã có SLA hiện tại.");
        }

        var normalizedPriority =
            NormalizePriority(feedback.Priority);

        var now = DateTime.UtcNow;

        var policy = await FindApplicablePolicyAsync(
            feedback.AreaId,
            feedback.CategoryId.Value,
            normalizedPriority,
            now);

        long createdFeedbackSlaId;

        _unitOfWork.BeginTransaction();

        try
        {
            var feedbackSla = new FeedbackSla
            {
                FeedbackId = feedback.FeedbackId,
                SlaPolicyId = policy.SlaPolicyId,

                AreaId = feedback.AreaId,
                CategoryId = feedback.CategoryId.Value,
                Priority = normalizedPriority,

                StartedAt = now,

                ResponseDueAt = now.AddMinutes(
                    policy.ResponseTimeMinutes),

                ResolutionDueAt = now.AddMinutes(
                    policy.ResolutionTimeMinutes),

                RespondedAt = null,
                ResolvedAt = null,

                TotalPausedMinutes = 0,

                Status = SlaStatus.Running,

                ResponseStatus =
                    SlaTargetStatus.Pending,

                ResolutionStatus =
                    SlaTargetStatus.Pending,

                IsResponseBreached = false,
                IsResolutionBreached = false,

                IsCurrent = true,

                StartedByUserId = startedByUserId,
                CompletedByUserId = null,

                CreatedAt = now,
                UpdatedAt = null
            };

            await _unitOfWork
                .GetRepository<FeedbackSla>()
                .AddAsync(feedbackSla);

            await _unitOfWork.SaveAsync();

            await AddEventAsync(
                feedbackSlaId: feedbackSla.FeedbackSlaId,
                eventType: SlaEventType.Started,
                oldStatus: null,
                newStatus: SlaStatus.Running,
                note:
                    $"SLA được bắt đầu theo policy " +
                    $"'{policy.PolicyName}'.",
                triggeredByUserId: startedByUserId,
                triggerSource: SlaTriggerSource.Staff);

            await _unitOfWork.SaveAsync();

            createdFeedbackSlaId =
                feedbackSla.FeedbackSlaId;

            _unitOfWork.CommitTransaction();
        }
        catch
        {
            _unitOfWork.RollBack();
            throw;
        }

        return await GetByIdAsync(
            createdFeedbackSlaId);
    }

    public async Task<FeedbackSlaDto>
        GetCurrentByFeedbackIdAsync(Guid feedbackId)
    {
        ValidateFeedbackId(feedbackId);

        var feedbackSlaId = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.FeedbackId == feedbackId &&
                x.IsCurrent)
            .Select(x => (long?)x.FeedbackSlaId)
            .FirstOrDefaultAsync();

        if (!feedbackSlaId.HasValue)
        {
            throw new KeyNotFoundException(
                "Feedback chưa có SLA hiện tại.");
        }

        return await GetByIdAsync(
            feedbackSlaId.Value);
    }

    public async Task<FeedbackSlaDto> GetByIdAsync(
        long feedbackSlaId)
    {
        ValidateFeedbackSlaId(feedbackSlaId);

        var entity = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Feedback)
            .Include(x => x.SlaPolicy)
            .Include(x => x.Area)
            .Include(x => x.Category)
            .Include(x => x.StartedByUser)
            .Include(x => x.CompletedByUser)
            .Include(x => x.SlaEvents)
                .ThenInclude(x => x.TriggeredByUser)
            .Include(x => x.SlaPauseHistories)
                .ThenInclude(x => x.PausedByUser)
            .Include(x => x.SlaPauseHistories)
                .ThenInclude(x => x.ResumedByUser)
            .FirstOrDefaultAsync(x =>
                x.FeedbackSlaId == feedbackSlaId);

        if (entity == null)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy SLA.");
        }

        return MapToDto(entity);
    }

    public async Task<FeedbackSlaDto> MarkRespondedAsync(
        Guid feedbackId,
        Guid triggeredByUserId,
        string? note)
    {
        ValidateFeedbackId(feedbackId);
        ValidateUserId(triggeredByUserId);

        await EnsureUserExistsAsync(triggeredByUserId);

        var entity =
            await GetCurrentEntityAsync(feedbackId);

        if (entity.Status == SlaStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Không thể ghi nhận phản hồi cho SLA đã bị hủy.");
        }

        if (entity.Status == SlaStatus.Completed)
        {
            throw new InvalidOperationException(
                "Không thể ghi nhận phản hồi cho SLA đã hoàn thành.");
        }

        if (entity.Status == SlaStatus.Paused)
        {
            throw new InvalidOperationException(
                "Không thể ghi nhận phản hồi khi SLA đang tạm dừng.");
        }

        if (entity.RespondedAt.HasValue)
        {
            throw new InvalidOperationException(
                "SLA đã được ghi nhận phản hồi đầu tiên.");
        }

        var now = DateTime.UtcNow;

        entity.RespondedAt = now;

        entity.ResponseStatus =
            now <= entity.ResponseDueAt
                ? SlaTargetStatus.Met
                : SlaTargetStatus.Breached;

        entity.IsResponseBreached =
            entity.ResponseStatus ==
            SlaTargetStatus.Breached;

        entity.UpdatedAt = now;

        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Responded,
            entity.Status,
            entity.Status,
            note ?? "Đã ghi nhận phản hồi đầu tiên.",
            triggeredByUserId,
            SlaTriggerSource.Manager);

        await _unitOfWork.SaveAsync();

        return await GetByIdAsync(
            entity.FeedbackSlaId);
    }

    public async Task<FeedbackSlaDto> PauseAsync(
        Guid feedbackId,
        Guid pausedByUserId,
        PauseSlaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateFeedbackId(feedbackId);
        ValidateUserId(pausedByUserId);
        ValidatePauseReason(request.ReasonCode);

        await EnsureUserExistsAsync(pausedByUserId);

        var entity =
            await GetCurrentEntityAsync(feedbackId);

        if (entity.Status != SlaStatus.Running)
        {
            throw new InvalidOperationException(
                "Chỉ SLA đang chạy mới có thể được tạm dừng.");
        }

        var hasOpenPause = await _unitOfWork
            .GetRepository<SlaPauseHistory>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x =>
                x.FeedbackSlaId ==
                entity.FeedbackSlaId &&
                !x.ResumedAt.HasValue);

        if (hasOpenPause)
        {
            throw new InvalidOperationException(
                "SLA đã có một lần tạm dừng chưa được tiếp tục.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = entity.Status;

        entity.Status = SlaStatus.Paused;
        entity.UpdatedAt = now;

        var pauseHistory = new SlaPauseHistory
        {
            FeedbackSlaId =
                entity.FeedbackSlaId,

            ReasonCode =
                NormalizePauseReason(
                    request.ReasonCode),

            ReasonNote =
                NormalizeOptionalText(
                    request.ReasonNote),

            PausedAt = now,
            ResumedAt = null,
            PausedMinutes = null,

            PausedByUserId = pausedByUserId,
            ResumedByUserId = null,

            CreatedAt = now,
            UpdatedAt = null
        };

        await _unitOfWork
            .GetRepository<SlaPauseHistory>()
            .AddAsync(pauseHistory);

        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Paused,
            oldStatus,
            SlaStatus.Paused,
            request.ReasonNote ??
            $"Tạm dừng SLA: {pauseHistory.ReasonCode}.",
            pausedByUserId,
            SlaTriggerSource.Manager);

        await _unitOfWork.SaveAsync();

        return await GetByIdAsync(
            entity.FeedbackSlaId);
    }

    public async Task<FeedbackSlaDto> ResumeAsync(
        Guid feedbackId,
        Guid resumedByUserId,
        ResumeSlaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateFeedbackId(feedbackId);
        ValidateUserId(resumedByUserId);

        await EnsureUserExistsAsync(resumedByUserId);

        var entity =
            await GetCurrentEntityAsync(feedbackId);

        if (entity.Status != SlaStatus.Paused)
        {
            throw new InvalidOperationException(
                "Chỉ SLA đang tạm dừng mới có thể được tiếp tục.");
        }

        var pauseHistory = await _unitOfWork
            .GetRepository<SlaPauseHistory>()
            .Entities
            .Where(x =>
                x.FeedbackSlaId ==
                entity.FeedbackSlaId &&
                !x.ResumedAt.HasValue)
            .OrderByDescending(x => x.PausedAt)
            .FirstOrDefaultAsync();

        if (pauseHistory == null)
        {
            throw new InvalidOperationException(
                "Không tìm thấy lịch sử tạm dừng đang mở.");
        }

        var now = DateTime.UtcNow;

        var pausedMinutes = Math.Max(
            1,
            (int)Math.Ceiling(
                (now - pauseHistory.PausedAt)
                .TotalMinutes));

        pauseHistory.ResumedAt = now;
        pauseHistory.PausedMinutes =
            pausedMinutes;

        pauseHistory.ResumedByUserId =
            resumedByUserId;

        pauseHistory.UpdatedAt = now;

        var oldStatus = entity.Status;

        entity.Status = SlaStatus.Running;

        entity.TotalPausedMinutes +=
            pausedMinutes;

        if (!entity.RespondedAt.HasValue)
        {
            entity.ResponseDueAt =
                entity.ResponseDueAt
                    .AddMinutes(pausedMinutes);
        }

        if (!entity.ResolvedAt.HasValue)
        {
            entity.ResolutionDueAt =
                entity.ResolutionDueAt
                    .AddMinutes(pausedMinutes);
        }

        entity.UpdatedAt = now;

        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Resumed,
            oldStatus,
            SlaStatus.Running,
            request.Note ??
            $"Tiếp tục SLA sau {pausedMinutes} phút tạm dừng.",
            resumedByUserId,
            SlaTriggerSource.Manager);

        await _unitOfWork.SaveAsync();

        return await GetByIdAsync(
            entity.FeedbackSlaId);
    }

    public async Task<FeedbackSlaDto> CompleteAsync(
        Guid feedbackId,
        Guid completedByUserId,
        CompleteSlaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateFeedbackId(feedbackId);
        ValidateUserId(completedByUserId);

        await EnsureUserExistsAsync(completedByUserId);

        var entity =
            await GetCurrentEntityAsync(feedbackId);

        if (entity.Status == SlaStatus.Completed)
        {
            throw new InvalidOperationException(
                "SLA đã được hoàn thành trước đó.");
        }

        if (entity.Status == SlaStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Không thể hoàn thành SLA đã bị hủy.");
        }

        if (entity.Status == SlaStatus.Paused)
        {
            throw new InvalidOperationException(
                "Cần tiếp tục SLA trước khi hoàn thành.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = entity.Status;

        entity.ResolvedAt = now;

        entity.CompletedByUserId =
            completedByUserId;

        entity.Status = SlaStatus.Completed;

        entity.ResolutionStatus =
            now <= entity.ResolutionDueAt
                ? SlaTargetStatus.Met
                : SlaTargetStatus.Breached;

        entity.IsResolutionBreached =
            entity.ResolutionStatus ==
            SlaTargetStatus.Breached;

        if (!entity.RespondedAt.HasValue)
        {
            entity.RespondedAt = now;

            entity.ResponseStatus =
                now <= entity.ResponseDueAt
                    ? SlaTargetStatus.Met
                    : SlaTargetStatus.Breached;

            entity.IsResponseBreached =
                entity.ResponseStatus ==
                SlaTargetStatus.Breached;
        }

        entity.UpdatedAt = now;

        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Completed,
            oldStatus,
            SlaStatus.Completed,
            request.Note ?? "SLA đã hoàn thành.",
            completedByUserId,
            SlaTriggerSource.Manager);

        await _unitOfWork.SaveAsync();

        return await GetByIdAsync(
            entity.FeedbackSlaId);
    }

    public async Task<FeedbackSlaDto> CancelAsync(
        Guid feedbackId,
        Guid cancelledByUserId,
        string? note)
    {
        ValidateFeedbackId(feedbackId);
        ValidateUserId(cancelledByUserId);

        await EnsureUserExistsAsync(cancelledByUserId);

        var entity =
            await GetCurrentEntityAsync(feedbackId);

        if (entity.Status == SlaStatus.Completed)
        {
            throw new InvalidOperationException(
                "Không thể hủy SLA đã hoàn thành.");
        }

        if (entity.Status == SlaStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "SLA đã bị hủy trước đó.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = entity.Status;

        entity.Status = SlaStatus.Cancelled;
        entity.UpdatedAt = now;

        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Cancelled,
            oldStatus,
            SlaStatus.Cancelled,
            note ?? "SLA đã bị hủy.",
            cancelledByUserId,
            SlaTriggerSource.Manager);

        await _unitOfWork.SaveAsync();

        return await GetByIdAsync(
            entity.FeedbackSlaId);
    }

    public async Task<FeedbackSlaDto> RecalculateAsync(
    Guid feedbackId,
    Guid recalculatedByUserId,
    RecalculateSlaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateFeedbackId(feedbackId);
        ValidateUserId(recalculatedByUserId);

        await EnsureUserExistsAsync(
            recalculatedByUserId);


        var entity =
            await GetCurrentEntityAsync(feedbackId);


        if (entity.Status == SlaStatus.Completed ||
            entity.Status == SlaStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Không thể tính lại SLA đã hoàn thành hoặc bị hủy.");
        }


        if (entity.Status == SlaStatus.Paused)
        {
            throw new InvalidOperationException(
                "Cần tiếp tục SLA trước khi tính lại deadline.");
        }


        /*
         * Lấy Category và Priority mới.
         * Nếu request không truyền thì giữ snapshot hiện tại.
         */
        var newCategoryId =
            request.CategoryId
            ?? entity.CategoryId;


        var newPriority =
            request.Priority
            ?? entity.Priority;



        /*
         * Tìm SLA Policy mới theo dữ liệu mới.
         */
        var policy =
            await FindApplicablePolicyAsync(
                entity.AreaId,
                newCategoryId,
                newPriority,
                DateTime.UtcNow);



        var oldPolicyId =
            entity.SlaPolicyId;


        var oldCategoryId =
            entity.CategoryId;


        var oldPriority =
            entity.Priority;


        var oldResponseDueAt =
            entity.ResponseDueAt;


        var oldResolutionDueAt =
            entity.ResolutionDueAt;



        /*
         * Update snapshot SLA
         */
        entity.SlaPolicyId =
            policy.SlaPolicyId;


        entity.CategoryId =
            newCategoryId;


        entity.Priority =
            newPriority;



        /*
         * Tính lại deadline
         */
        entity.ResponseDueAt =
            entity.StartedAt
                .AddMinutes(
                    policy.ResponseTimeMinutes)
                .AddMinutes(
                    entity.TotalPausedMinutes);



        entity.ResolutionDueAt =
            entity.StartedAt
                .AddMinutes(
                    policy.ResolutionTimeMinutes)
                .AddMinutes(
                    entity.TotalPausedMinutes);



        var now =
            DateTime.UtcNow;



        /*
         * Update Response SLA status
         */
        if (!entity.RespondedAt.HasValue)
        {
            entity.ResponseStatus =
                now > entity.ResponseDueAt
                    ? SlaTargetStatus.Breached
                    : SlaTargetStatus.Pending;
        }
        else
        {
            entity.ResponseStatus =
                entity.RespondedAt.Value <= entity.ResponseDueAt
                    ? SlaTargetStatus.Met
                    : SlaTargetStatus.Breached;
        }



        entity.IsResponseBreached =
            entity.ResponseStatus ==
            SlaTargetStatus.Breached;



        /*
         * Update Resolution SLA status
         */
        if (!entity.ResolvedAt.HasValue)
        {
            entity.ResolutionStatus =
                now > entity.ResolutionDueAt
                    ? SlaTargetStatus.Breached
                    : SlaTargetStatus.Pending;
        }
        else
        {
            entity.ResolutionStatus =
                entity.ResolvedAt.Value <= entity.ResolutionDueAt
                    ? SlaTargetStatus.Met
                    : SlaTargetStatus.Breached;
        }



        entity.IsResolutionBreached =
            entity.ResolutionStatus ==
            SlaTargetStatus.Breached;



        entity.UpdatedAt =
            now;



        var eventNote =
            request.Note
            ??
            $"Tính lại SLA. " +
            $"Policy: {oldPolicyId} → {policy.SlaPolicyId}. " +
            $"Category: {oldCategoryId} → {newCategoryId}. " +
            $"Priority: {oldPriority} → {newPriority}. " +
            $"ResponseDueAt: {oldResponseDueAt:O} → {entity.ResponseDueAt:O}. " +
            $"ResolutionDueAt: {oldResolutionDueAt:O} → {entity.ResolutionDueAt:O}.";



        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Recalculated,
            entity.Status,
            entity.Status,
            eventNote,
            recalculatedByUserId,
            SlaTriggerSource.Staff);



        await _unitOfWork.SaveAsync();



        return await GetByIdAsync(
            entity.FeedbackSlaId);
    }

    public async Task CheckViolationAsync(
        long feedbackSlaId)
    {
        ValidateFeedbackSlaId(feedbackSlaId);

        var entity = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsSplitQuery()
            .Include(x => x.Feedback)
                .ThenInclude(x => x.FeedbackProviderReports)
                    .ThenInclude(x => x.Coordinator)
            .FirstOrDefaultAsync(x =>
                x.FeedbackSlaId == feedbackSlaId);

        if (entity == null)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy SLA.");
        }

        if (!entity.IsCurrent ||
            entity.Status != SlaStatus.Running)
        {
            return;
        }

        var result =
            await ApplyMonitoringCheckAsync(entity);

        if (!result.HasChanges)
        {
            return;
        }

        await _unitOfWork.SaveAsync();

        await SendMonitoringNotificationsAsync(
            entity,
            result);
    }

    public async Task<int> CheckAllRunningSlasAsync()
    {
        var runningSlas = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsSplitQuery()
            .Include(x => x.Feedback)
                .ThenInclude(x => x.FeedbackProviderReports)
                    .ThenInclude(x => x.Coordinator)
            .Where(x =>
                x.IsCurrent &&
                x.Status == SlaStatus.Running)
            .ToListAsync();

        if (runningSlas.Count == 0)
        {
            _logger.LogDebug(
                "Không có SLA đang chạy cần kiểm tra.");

            return 0;
        }

        var updatedCount = 0;

        foreach (var entity in runningSlas)
        {
            try
            {
                var result =
                    await ApplyMonitoringCheckAsync(entity);

                if (!result.HasChanges)
                {
                    continue;
                }

                /*
                 * Lưu trạng thái SLA và SlaEvent trước.
                 * Chỉ gửi email/notification sau khi SaveAsync thành công.
                 */
                await _unitOfWork.SaveAsync();

                updatedCount++;

                await SendMonitoringNotificationsAsync(
                    entity,
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Không thể kiểm tra SLA {FeedbackSlaId}.",
                    entity.FeedbackSlaId);
            }
        }

        _logger.LogInformation(
            "Đã cập nhật {UpdatedCount} SLA có cảnh báo hoặc vi phạm.",
            updatedCount);

        return updatedCount;
    }

    private async Task<SlaMonitoringCheckResult>
        ApplyMonitoringCheckAsync(
            FeedbackSla entity)
    {
        var now = DateTime.UtcNow;

        var result =
            new SlaMonitoringCheckResult();

        var thresholdPercent = Math.Clamp(
            _slaOptions.WarningThresholdPercent,
            1,
            99);

        /*
         * RESPONSE WARNING
         *
         * WarningThresholdPercent = 30 nghĩa là cảnh báo
         * khi SLA chỉ còn khoảng 30% tổng thời gian phản hồi.
         */
        if (!entity.RespondedAt.HasValue &&
            !entity.IsResponseBreached &&
            now <= entity.ResponseDueAt)
        {
            var totalResponseMinutes =
                (entity.ResponseDueAt - entity.StartedAt)
                .TotalMinutes;

            if (totalResponseMinutes > 0)
            {
                var responseWarningAt =
                    entity.StartedAt.AddMinutes(
                        totalResponseMinutes *
                        (100 - thresholdPercent) / 100d);

                var hasResponseWarning =
                    await HasSlaEventAsync(
                        entity.FeedbackSlaId,
                        SlaEventType.ResponseWarning);

                if (now >= responseWarningAt &&
    !hasResponseWarning)
                {
                    entity.UpdatedAt = now;

                    await AddEventAsync(
                        entity.FeedbackSlaId,
                        SlaEventType.ResponseWarning,
                        entity.Status,
                        entity.Status,
                        $"SLA phản hồi chỉ còn khoảng " +
                        $"{thresholdPercent}% thời gian.",
                        null,
                        SlaTriggerSource.System);

                    result.ResponseWarningCreated = true;
                }
            }
        }

        /*
         * RESOLUTION WARNING
         */
        if (!entity.ResolvedAt.HasValue &&
            !entity.IsResolutionBreached &&
            now <= entity.ResolutionDueAt)
        {
            var totalResolutionMinutes =
                (entity.ResolutionDueAt - entity.StartedAt)
                .TotalMinutes;

            if (totalResolutionMinutes > 0)
            {
                var resolutionWarningAt =
                    entity.StartedAt.AddMinutes(
                        totalResolutionMinutes *
                        (100 - thresholdPercent) / 100d);

                var hasResolutionWarning =
                    await HasSlaEventAsync(
                        entity.FeedbackSlaId,
                        SlaEventType.ResolutionWarning);

                if (now >= resolutionWarningAt &&
    !hasResolutionWarning)
                {
                    entity.UpdatedAt = now;

                    await AddEventAsync(
                        entity.FeedbackSlaId,
                        SlaEventType.ResolutionWarning,
                        entity.Status,
                        entity.Status,
                        $"SLA hoàn thành xử lý chỉ còn khoảng " +
                        $"{thresholdPercent}% thời gian.",
                        null,
                        SlaTriggerSource.System);

                    result.ResolutionWarningCreated = true;
                }
            }
        }

        /*
         * RESPONSE BREACH
         */
        if (!entity.RespondedAt.HasValue &&
            !entity.IsResponseBreached &&
            now > entity.ResponseDueAt)
        {
            entity.ResponseStatus =
                SlaTargetStatus.Breached;

            entity.IsResponseBreached = true;
            entity.UpdatedAt = now;

            await AddEventAsync(
                entity.FeedbackSlaId,
                SlaEventType.ResponseBreached,
                entity.Status,
                entity.Status,
                "SLA đã vi phạm thời hạn phản hồi đầu tiên.",
                null,
                SlaTriggerSource.System);

            result.ResponseJustBreached = true;

            _logger.LogWarning(
                "SLA {FeedbackSlaId} của feedback {FeedbackId} đã quá hạn phản hồi.",
                entity.FeedbackSlaId,
                entity.FeedbackId);
        }

        /*
         * RESOLUTION BREACH
         */
        if (!entity.ResolvedAt.HasValue &&
            !entity.IsResolutionBreached &&
            now > entity.ResolutionDueAt)
        {
            entity.ResolutionStatus =
                SlaTargetStatus.Breached;

            entity.IsResolutionBreached = true;
            entity.UpdatedAt = now;

            await AddEventAsync(
                entity.FeedbackSlaId,
                SlaEventType.ResolutionBreached,
                entity.Status,
                entity.Status,
                "SLA đã vi phạm thời hạn hoàn thành xử lý.",
                null,
                SlaTriggerSource.System);

            result.ResolutionJustBreached = true;

            _logger.LogWarning(
                "SLA {FeedbackSlaId} của feedback {FeedbackId} đã quá hạn xử lý.",
                entity.FeedbackSlaId,
                entity.FeedbackId);
        }

        return result;
    }

    private async Task SendMonitoringNotificationsAsync(
        FeedbackSla entity,
        SlaMonitoringCheckResult result)
    {
        if (result.ResponseWarningCreated)
        {
            await SendWarningEmailToProviderAsync(
                entity,
                SlaEventType.ResponseWarning);
        }

        if (result.ResolutionWarningCreated)
        {
            await SendWarningEmailToProviderAsync(
                entity,
                SlaEventType.ResolutionWarning);
        }

        await SendBreachNotificationsSafeAsync(
            entity,
            result.ResponseJustBreached,
            result.ResolutionJustBreached);
    }

    private async Task<bool> HasSlaEventAsync(
        long feedbackSlaId,
        string eventType)
    {
        return await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x =>
                x.FeedbackSlaId == feedbackSlaId &&
                x.EventType == eventType);
    }

    private async Task SendWarningEmailToProviderAsync(
        FeedbackSla entity,
        string warningType)
    {
        if (entity.Feedback == null)
        {
            _logger.LogWarning(
                "Không thể gửi SLA warning email vì chưa load feedback. SLA: {FeedbackSlaId}.",
                entity.FeedbackSlaId);

            return;
        }

        /*
         * FeedbackProviderReport mới nhất được xem là assignment
         * provider hiện tại của feedback.
         */
        var providerReport = entity.Feedback
            .FeedbackProviderReports
            .Where(x =>
                x.Coordinator != null &&
                x.Coordinator.IsActive)
            .OrderByDescending(x => x.ReportedAt)
            .ThenByDescending(x => x.ProviderReportId)
            .FirstOrDefault();

        if (providerReport == null)
        {
            _logger.LogWarning(
                "Feedback {FeedbackId} chưa được gán cho provider coordinator.",
                entity.FeedbackId);

            return;
        }

        var coordinator =
            providerReport.Coordinator;

        if (string.IsNullOrWhiteSpace(
                coordinator.Email))
        {
            _logger.LogWarning(
                "Provider coordinator {CoordinatorId} của feedback {FeedbackId} chưa có email.",
                coordinator.CoordinatorId,
                entity.FeedbackId);

            return;
        }

        var isResponseWarning =
            warningType ==
            SlaEventType.ResponseWarning;

        var deadlineUtc =
            isResponseWarning
                ? entity.ResponseDueAt
                : entity.ResolutionDueAt;

        var warningLabel =
            isResponseWarning
                ? "phản hồi lần đầu"
                : "hoàn thành xử lý";

        var subject =
            isResponseWarning
                ? "[UrbanService] Cảnh báo yêu cầu sắp hết hạn phản hồi SLA"
                : "[UrbanService] Cảnh báo yêu cầu sắp hết hạn xử lý SLA";

        var htmlBody =
            BuildSlaWarningEmailHtml(
                coordinatorName:
                    WebUtility.HtmlEncode(
                        coordinator.CoordinatorName),
                providerName:
                    WebUtility.HtmlEncode(
                        coordinator.ProviderName),
                feedbackId:
                    entity.FeedbackId.ToString(),
                feedbackTitle:
                    WebUtility.HtmlEncode(
                        entity.Feedback.Title),
                locationText:
                    WebUtility.HtmlEncode(
                        entity.Feedback.LocationText),
                priority:
                    WebUtility.HtmlEncode(
                        entity.Priority),
                reportStatus:
                    WebUtility.HtmlEncode(
                        providerReport.ReportStatus),
                warningLabel:
                    WebUtility.HtmlEncode(
                        warningLabel),
                deadlineDisplay:
                    FormatVietnamDateTime(
                        deadlineUtc),
                warningThresholdPercent:
                    Math.Clamp(
                        _slaOptions.WarningThresholdPercent,
                        1,
                        99));

        try
        {
            await _emailSender.SendAsync(
                new EmailMessageDto
                {
                    To =
                    [
                        coordinator.Email.Trim()
                    ],
                    Subject = subject,
                    Body = htmlBody,
                    IsHtml = true
                });

            _logger.LogInformation(
                "Đã gửi {WarningType} email đến coordinator {CoordinatorId} cho feedback {FeedbackId}.",
                warningType,
                coordinator.CoordinatorId,
                entity.FeedbackId);
        }
        catch (Exception ex)
        {
            /*
             * Email lỗi không được làm worker dừng.
             */
            _logger.LogError(
                ex,
                "Không thể gửi {WarningType} email đến coordinator {CoordinatorId}, email {Email}, feedback {FeedbackId}.",
                warningType,
                coordinator.CoordinatorId,
                coordinator.Email,
                entity.FeedbackId);
        }
    }

    private async Task SendBreachNotificationsSafeAsync(
        FeedbackSla entity,
        bool responseJustBreached,
        bool resolutionJustBreached)
    {
        if (!responseJustBreached &&
            !resolutionJustBreached)
        {
            return;
        }

        if (entity.Feedback == null)
        {
            _logger.LogWarning(
                "Không thể gửi email vi phạm SLA vì chưa load feedback. SLA: {FeedbackSlaId}.",
                entity.FeedbackSlaId);

            return;
        }

        /*
         * FeedbackProviderReport mới nhất được xem là assignment
         * provider hiện tại của feedback.
         */
        var providerReport = entity.Feedback
            .FeedbackProviderReports
            .Where(x =>
                x.Coordinator != null &&
                x.Coordinator.IsActive)
            .OrderByDescending(x => x.ReportedAt)
            .ThenByDescending(x => x.ProviderReportId)
            .FirstOrDefault();

        if (providerReport == null)
        {
            _logger.LogWarning(
                "Feedback {FeedbackId} chưa có provider coordinator để nhận email vi phạm SLA.",
                entity.FeedbackId);

            return;
        }

        var coordinator = providerReport.Coordinator;

        if (string.IsNullOrWhiteSpace(coordinator.Email))
        {
            _logger.LogWarning(
                "Provider coordinator {CoordinatorId} của feedback {FeedbackId} chưa có email.",
                coordinator.CoordinatorId,
                entity.FeedbackId);

            return;
        }

        if (responseJustBreached)
        {
            await SendBreachEmailToProviderAsync(
                entity,
                providerReport,
                SlaEventType.ResponseBreached);
        }

        if (resolutionJustBreached)
        {
            await SendBreachEmailToProviderAsync(
                entity,
                providerReport,
                SlaEventType.ResolutionBreached);
        }
    }

    private async Task SendBreachEmailToProviderAsync(
        FeedbackSla entity,
        FeedbackProviderReport providerReport,
        string breachType)
    {
        var coordinator = providerReport.Coordinator;

        if (string.IsNullOrWhiteSpace(coordinator.Email))
        {
            return;
        }

        var isResponseBreach =
            breachType == SlaEventType.ResponseBreached;

        var deadlineUtc =
            isResponseBreach
                ? entity.ResponseDueAt
                : entity.ResolutionDueAt;

        var breachLabel =
            isResponseBreach
                ? "phản hồi lần đầu"
                : "hoàn thành xử lý";

        var subject =
            isResponseBreach
                ? "[UrbanService] Yêu cầu đã vi phạm thời hạn phản hồi SLA"
                : "[UrbanService] Yêu cầu đã vi phạm thời hạn xử lý SLA";

        var htmlBody =
            BuildSlaBreachEmailHtml(
                coordinatorName:
                    WebUtility.HtmlEncode(
                        coordinator.CoordinatorName),
                providerName:
                    WebUtility.HtmlEncode(
                        coordinator.ProviderName),
                feedbackId:
                    entity.FeedbackId.ToString(),
                feedbackTitle:
                    WebUtility.HtmlEncode(
                        entity.Feedback.Title),
                locationText:
                    WebUtility.HtmlEncode(
                        entity.Feedback.LocationText),
                priority:
                    WebUtility.HtmlEncode(
                        entity.Priority),
                reportStatus:
                    WebUtility.HtmlEncode(
                        providerReport.ReportStatus),
                breachLabel:
                    WebUtility.HtmlEncode(
                        breachLabel),
                deadlineDisplay:
                    FormatVietnamDateTime(
                        deadlineUtc),
                breachedAtDisplay:
                    FormatVietnamDateTime(
                        DateTime.UtcNow));

        try
        {
            await _emailSender.SendAsync(
                new EmailMessageDto
                {
                    To =
                    [
                        coordinator.Email.Trim()
                    ],
                    Subject = subject,
                    Body = htmlBody,
                    IsHtml = true
                });

            _logger.LogInformation(
                "Đã gửi email {BreachType} đến coordinator {CoordinatorId} cho feedback {FeedbackId}.",
                breachType,
                coordinator.CoordinatorId,
                entity.FeedbackId);
        }
        catch (Exception ex)
        {
            /*
             * Email lỗi không được làm worker dừng.
             */
            _logger.LogError(
                ex,
                "Không thể gửi email {BreachType} đến coordinator {CoordinatorId}, email {Email}, feedback {FeedbackId}.",
                breachType,
                coordinator.CoordinatorId,
                coordinator.Email,
                entity.FeedbackId);
        }
    }

    private static string BuildSlaBreachEmailHtml(
        string coordinatorName,
        string providerName,
        string feedbackId,
        string feedbackTitle,
        string locationText,
        string priority,
        string reportStatus,
        string breachLabel,
        string deadlineDisplay,
        string breachedAtDisplay)
    {
        var labelCellStyle =
            "width:34%;" +
            "padding:11px 12px;" +
            "background:#f9fafb;" +
            "border:1px solid #e5e7eb;" +
            "font-weight:600;" +
            "vertical-align:top;";

        var valueCellStyle =
            "padding:11px 12px;" +
            "border:1px solid #e5e7eb;" +
            "vertical-align:top;";

        return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport"
          content="width=device-width, initial-scale=1.0">
</head>
<body style="
    margin:0;
    padding:0;
    background-color:#f4f6f8;
    font-family:Arial, Helvetica, sans-serif;
    color:#1f2937;">

    <table width="100%"
           cellpadding="0"
           cellspacing="0"
           role="presentation"
           style="background-color:#f4f6f8;
                  padding:24px 12px;">
        <tr>
            <td align="center">
                <table width="640"
                       cellpadding="0"
                       cellspacing="0"
                       role="presentation"
                       style="
                           width:100%;
                           max-width:640px;
                           background:#ffffff;
                           border-radius:12px;
                           overflow:hidden;
                           box-shadow:0 4px 16px rgba(0,0,0,0.08);">

                    <tr>
                        <td style="
                            background:#dc2626;
                            color:#ffffff;
                            padding:22px 28px;">
                            <div style="
                                font-size:22px;
                                font-weight:700;">
                                UrbanService
                            </div>
                            <div style="
                                margin-top:6px;
                                font-size:15px;">
                                Thông báo vi phạm SLA
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td style="padding:28px;">
                            <p style="
                                margin:0 0 16px;
                                font-size:16px;">
                                Kính gửi
                                <strong>{{coordinatorName}}</strong>,
                            </p>

                            <p style="
                                margin:0 0 20px;
                                line-height:1.6;
                                font-size:15px;">
                                Hệ thống UrbanService ghi nhận yêu cầu
                                được giao cho
                                <strong>{{providerName}}</strong>
                                đã vượt quá thời hạn
                                <strong>{{breachLabel}}</strong>
                                theo chính sách SLA.
                            </p>

                            <div style="
                                background:#fef2f2;
                                border-left:5px solid #dc2626;
                                padding:16px 18px;
                                margin-bottom:22px;
                                border-radius:6px;">
                                <strong style="color:#991b1b;">
                                    Vi phạm SLA:
                                </strong>
                                Yêu cầu đã vượt quá thời hạn cam kết.
                                Đề nghị kiểm tra và xử lý ngay.
                            </div>

                            <table width="100%"
                                   cellpadding="0"
                                   cellspacing="0"
                                   role="presentation"
                                   style="
                                       border-collapse:collapse;
                                       font-size:14px;
                                       margin-bottom:24px;">
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Mã phản ánh
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{feedbackId}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Tiêu đề
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{feedbackTitle}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Địa điểm
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{locationText}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Mức ưu tiên
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{priority}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Trạng thái báo cáo
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{reportStatus}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Loại thời hạn
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{breachLabel}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Hạn SLA
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        <strong style="color:#dc2626;">
                                            {{deadlineDisplay}}
                                        </strong>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Thời điểm ghi nhận vi phạm
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{breachedAtDisplay}}
                                    </td>
                                </tr>
                            </table>

                            <p style="
                                margin:0 0 16px;
                                line-height:1.6;
                                font-size:15px;">
                                Đề nghị Quý đơn vị khẩn trương kiểm tra,
                                cập nhật tiến độ và hoàn thành xử lý.
                                Vi phạm này đã được ghi nhận trong lịch sử SLA
                                của hệ thống.
                            </p>

                            <p style="
                                margin:24px 0 0;
                                line-height:1.6;
                                font-size:15px;">
                                Trân trọng,<br>
                                <strong>UrbanService System</strong>
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style="
                            background:#f9fafb;
                            padding:16px 28px;
                            color:#6b7280;
                            font-size:12px;
                            line-height:1.5;">
                            Đây là email tự động từ hệ thống UrbanService.
                            Vui lòng không trả lời trực tiếp email này.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
""";
    }

    private static string BuildSlaWarningEmailHtml(
        string coordinatorName,
        string providerName,
        string feedbackId,
        string feedbackTitle,
        string locationText,
        string priority,
        string reportStatus,
        string warningLabel,
        string deadlineDisplay,
        int warningThresholdPercent)
    {
        var labelCellStyle =
            "width:34%;" +
            "padding:11px 12px;" +
            "background:#f9fafb;" +
            "border:1px solid #e5e7eb;" +
            "font-weight:600;" +
            "vertical-align:top;";

        var valueCellStyle =
            "padding:11px 12px;" +
            "border:1px solid #e5e7eb;" +
            "vertical-align:top;";

        return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport"
          content="width=device-width, initial-scale=1.0">
</head>
<body style="
    margin:0;
    padding:0;
    background-color:#f4f6f8;
    font-family:Arial, Helvetica, sans-serif;
    color:#1f2937;">

    <table width="100%"
           cellpadding="0"
           cellspacing="0"
           role="presentation"
           style="background-color:#f4f6f8;
                  padding:24px 12px;">
        <tr>
            <td align="center">
                <table width="640"
                       cellpadding="0"
                       cellspacing="0"
                       role="presentation"
                       style="
                           width:100%;
                           max-width:640px;
                           background:#ffffff;
                           border-radius:12px;
                           overflow:hidden;
                           box-shadow:0 4px 16px rgba(0,0,0,0.08);">

                    <tr>
                        <td style="
                            background:#f59e0b;
                            color:#ffffff;
                            padding:22px 28px;">
                            <div style="
                                font-size:22px;
                                font-weight:700;">
                                UrbanService
                            </div>
                            <div style="
                                margin-top:6px;
                                font-size:15px;">
                                Cảnh báo thời hạn SLA
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td style="padding:28px;">
                            <p style="
                                margin:0 0 16px;
                                font-size:16px;">
                                Kính gửi
                                <strong>{{coordinatorName}}</strong>,
                            </p>

                            <p style="
                                margin:0 0 20px;
                                line-height:1.6;
                                font-size:15px;">
                                Hệ thống UrbanService ghi nhận yêu cầu
                                được giao cho
                                <strong>{{providerName}}</strong>
                                đang gần hết thời hạn
                                <strong>{{warningLabel}}</strong>
                                theo chính sách SLA.
                            </p>

                            <div style="
                                background:#fff7ed;
                                border-left:5px solid #f59e0b;
                                padding:16px 18px;
                                margin-bottom:22px;
                                border-radius:6px;">
                                <strong style="color:#b45309;">
                                    Cảnh báo:
                                </strong>
                                Yêu cầu hiện chỉ còn khoảng
                                <strong>{{warningThresholdPercent}}%</strong>
                                thời gian SLA.
                            </div>

                            <table width="100%"
                                   cellpadding="0"
                                   cellspacing="0"
                                   role="presentation"
                                   style="
                                       border-collapse:collapse;
                                       font-size:14px;
                                       margin-bottom:24px;">
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Mã phản ánh
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{feedbackId}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Tiêu đề
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{feedbackTitle}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Địa điểm
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{locationText}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Mức ưu tiên
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{priority}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Trạng thái báo cáo
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{reportStatus}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Loại thời hạn
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{warningLabel}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Hạn SLA
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        <strong style="color:#dc2626;">
                                            {{deadlineDisplay}}
                                        </strong>
                                    </td>
                                </tr>
                            </table>

                            <p style="
                                margin:0 0 16px;
                                line-height:1.6;
                                font-size:15px;">
                                Đề nghị Quý đơn vị kiểm tra tiến độ,
                                cập nhật trạng thái và hoàn thành xử lý
                                trước thời hạn trên để tránh phát sinh
                                vi phạm SLA.
                            </p>

                            <p style="
                                margin:24px 0 0;
                                line-height:1.6;
                                font-size:15px;">
                                Trân trọng,<br>
                                <strong>UrbanService System</strong>
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style="
                            background:#f9fafb;
                            padding:16px 28px;
                            color:#6b7280;
                            font-size:12px;
                            line-height:1.5;">
                            Đây là email tự động từ hệ thống UrbanService.
                            Vui lòng không trả lời trực tiếp email này.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
""";
    }

    private static string FormatVietnamDateTime(
        DateTime utcDateTime)
    {
        var utcValue =
            utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(
                    utcDateTime,
                    DateTimeKind.Utc);

        TimeZoneInfo vietnamTimeZone;

        try
        {
            /*
             * Linux / Docker.
             */
            vietnamTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            /*
             * Windows.
             */
            vietnamTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "SE Asia Standard Time");
        }

        var vietnamTime =
            TimeZoneInfo.ConvertTimeFromUtc(
                utcValue,
                vietnamTimeZone);

        return vietnamTime.ToString(
            "dd/MM/yyyy HH:mm");
    }

    private sealed class SlaMonitoringCheckResult
    {
        public bool ResponseWarningCreated { get; set; }

        public bool ResolutionWarningCreated { get; set; }

        public bool ResponseJustBreached { get; set; }

        public bool ResolutionJustBreached { get; set; }

        public bool HasChanges =>
            ResponseWarningCreated ||
            ResolutionWarningCreated ||
            ResponseJustBreached ||
            ResolutionJustBreached;
    }

    private async Task<SlaPolicy>
        FindApplicablePolicyAsync(
            int areaId,
            int categoryId,
            string priority,
            DateTime effectiveAt)
    {
        var normalizedPriority =
            NormalizePriority(priority);

        var policy = await _unitOfWork
            .GetRepository<SlaPolicy>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Priority == normalizedPriority &&
                x.EffectiveFrom <= effectiveAt &&
                (
                    !x.EffectiveTo.HasValue ||
                    x.EffectiveTo.Value >= effectiveAt
                ) &&
                (
                    !x.AreaId.HasValue ||
                    x.AreaId.Value == areaId
                ) &&
                (
                    !x.CategoryId.HasValue ||
                    x.CategoryId.Value == categoryId
                ))
            .OrderByDescending(x =>
                x.AreaId.HasValue &&
                x.CategoryId.HasValue)
            .ThenByDescending(x =>
                x.AreaId.HasValue)
            .ThenByDescending(x =>
                x.CategoryId.HasValue)
            .ThenByDescending(x =>
                x.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (policy == null)
        {
            throw new InvalidOperationException(
                "Không tìm thấy SLA policy phù hợp với khu vực, " +
                "category và priority của feedback.");
        }

        return policy;
    }

    private async Task<FeedbackSla>
        GetCurrentEntityAsync(Guid feedbackId)
    {
        var entity = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .FirstOrDefaultAsync(x =>
                x.FeedbackId == feedbackId &&
                x.IsCurrent);

        if (entity == null)
        {
            throw new KeyNotFoundException(
                "Feedback chưa có SLA hiện tại.");
        }

        return entity;
    }

    private async Task AddEventAsync(
        long feedbackSlaId,
        string eventType,
        string? oldStatus,
        string? newStatus,
        string? note,
        Guid? triggeredByUserId,
        string triggerSource)
    {
        var entity = new SlaEvent
        {
            FeedbackSlaId = feedbackSlaId,
            EventType = eventType,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = NormalizeOptionalText(note),
            TriggeredByUserId =
                triggeredByUserId,
            TriggerSource = triggerSource,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork
            .GetRepository<SlaEvent>()
            .AddAsync(entity);
    }

    private async Task EnsureUserExistsAsync(
        Guid userId)
    {
        var exists = await _unitOfWork
            .GetRepository<User>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == userId);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy người dùng thực hiện thao tác.");
        }
    }

    private static FeedbackSlaDto MapToDto(
        FeedbackSla entity)
    {
        var now = DateTime.UtcNow;

        double? remainingResponseMinutes = null;
        double? remainingResolutionMinutes = null;

        if (!entity.RespondedAt.HasValue &&
            entity.Status == SlaStatus.Running)
        {
            remainingResponseMinutes =
                Math.Round(
                    (entity.ResponseDueAt - now)
                    .TotalMinutes,
                    2);
        }

        if (!entity.ResolvedAt.HasValue &&
            entity.Status == SlaStatus.Running)
        {
            remainingResolutionMinutes =
                Math.Round(
                    (entity.ResolutionDueAt - now)
                    .TotalMinutes,
                    2);
        }

        return new FeedbackSlaDto
        {
            FeedbackSlaId =
                entity.FeedbackSlaId,

            FeedbackId =
                entity.FeedbackId,

            FeedbackTitle =
                entity.Feedback?.Title,

            SlaPolicyId =
                entity.SlaPolicyId,

            PolicyName =
                entity.SlaPolicy?.PolicyName,

            AreaId =
                entity.AreaId,

            AreaName =
                entity.Area?.AreaName,

            CategoryId =
                entity.CategoryId,

            CategoryName =
                entity.Category?.CategoryName,

            Priority =
                entity.Priority,

            StartedAt =
                entity.StartedAt,

            ResponseDueAt =
                entity.ResponseDueAt,

            ResolutionDueAt =
                entity.ResolutionDueAt,

            RespondedAt =
                entity.RespondedAt,

            ResolvedAt =
                entity.ResolvedAt,

            TotalPausedMinutes =
                entity.TotalPausedMinutes,

            Status =
                entity.Status,

            ResponseStatus =
                entity.ResponseStatus,

            ResolutionStatus =
                entity.ResolutionStatus,

            IsResponseBreached =
                entity.IsResponseBreached,

            IsResolutionBreached =
                entity.IsResolutionBreached,

            IsCurrent =
                entity.IsCurrent,

            StartedByUserId =
                entity.StartedByUserId,

            StartedByUserName =
                entity.StartedByUser?.FullName,

            CompletedByUserId =
                entity.CompletedByUserId,

            CompletedByUserName =
                entity.CompletedByUser?.FullName,

            CreatedAt =
                entity.CreatedAt,

            UpdatedAt =
                entity.UpdatedAt,

            RemainingResponseMinutes =
                remainingResponseMinutes,

            RemainingResolutionMinutes =
                remainingResolutionMinutes,

            Events = entity.SlaEvents
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Select(x => new SlaEventDto
                {
                    SlaEventId =
                        x.SlaEventId,

                    FeedbackSlaId =
                        x.FeedbackSlaId,

                    EventType =
                        x.EventType,

                    OldStatus =
                        x.OldStatus,

                    NewStatus =
                        x.NewStatus,

                    Note =
                        x.Note,

                    TriggeredByUserId =
                        x.TriggeredByUserId,

                    TriggeredByUserName =
                        x.TriggeredByUser?.FullName,

                    TriggerSource =
                        x.TriggerSource,

                    CreatedAt =
                        x.CreatedAt
                })
                .ToArray(),

            PauseHistories =
                entity.SlaPauseHistories
                    .OrderByDescending(x =>
                        x.PausedAt)
                    .Select(x =>
                        new SlaPauseHistoryDto
                        {
                            SlaPauseHistoryId =
                                x.SlaPauseHistoryId,

                            FeedbackSlaId =
                                x.FeedbackSlaId,

                            ReasonCode =
                                x.ReasonCode,

                            ReasonNote =
                                x.ReasonNote,

                            PausedAt =
                                x.PausedAt,

                            ResumedAt =
                                x.ResumedAt,

                            PausedMinutes =
                                x.PausedMinutes,

                            PausedByUserId =
                                x.PausedByUserId,

                            PausedByUserName =
                                x.PausedByUser?.FullName,

                            ResumedByUserId =
                                x.ResumedByUserId,

                            ResumedByUserName =
                                x.ResumedByUser?.FullName,

                            CreatedAt =
                                x.CreatedAt,

                            UpdatedAt =
                                x.UpdatedAt
                        })
                    .ToArray()
        };
    }

    private static void ValidateFeedbackId(
            Guid feedbackId)
    {
        if (feedbackId == Guid.Empty)
        {
            throw new ArgumentException(
                "Feedback ID không hợp lệ.");
        }
    }

    private static void ValidateFeedbackSlaId(
        long feedbackSlaId)
    {
        if (feedbackSlaId <= 0)
        {
            throw new ArgumentException(
                "Feedback SLA ID không hợp lệ.");
        }
    }

    private static void ValidateUserId(
        Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID không hợp lệ.");
        }
    }

    private static void ValidatePauseReason(
        string reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new ArgumentException(
                "Lý do tạm dừng là bắt buộc.");
        }

        var isValid =
            SlaPauseReason.All.Any(x =>
                string.Equals(
                    x,
                    reasonCode.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        if (!isValid)
        {
            throw new ArgumentException(
                "ReasonCode không hợp lệ. " +
                $"Giá trị hợp lệ: " +
                $"{string.Join(", ", SlaPauseReason.All)}.");
        }
    }

    private static string NormalizePauseReason(
        string reasonCode)
    {
        return SlaPauseReason.All.First(x =>
            string.Equals(
                x,
                reasonCode.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePriority(
        string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException(
                "Priority là bắt buộc.");
        }

        var normalized = priority.Trim();

        if (normalized.Equals(
                "Low",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Low";
        }

        if (normalized.Equals(
                "Medium",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Medium";
        }

        if (normalized.Equals(
                "High",
                StringComparison.OrdinalIgnoreCase))
        {
            return "High";
        }

        if (normalized.Equals(
            "Urgent",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Urgent";
        }

        throw new ArgumentException(
            "Priority không hợp lệ. " +
            "Chỉ chấp nhận Low, Medium, High hoặc Urgent.");
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static double CalculatePercent(
    double used,
    double total)
    {
        if (total <= 0)
        {
            return 100;
        }


        var value =
            used / total * 100;


        return Math.Round(
            Math.Min(
                100,
                Math.Max(
                    0,
                    value)),
            2);
    }
}
