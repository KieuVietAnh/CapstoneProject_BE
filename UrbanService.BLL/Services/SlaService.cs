using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Helpers;
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
    private readonly ISlaRealtimeSender _slaRealtimeSender;

    public SlaService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    IEmailSender emailSender,
    ILogger<SlaService> logger,
    IOptions<SlaMonitoringOptions> slaOptions,
    ISlaRealtimeSender slaRealtimeSender)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _logger = logger;
        _slaOptions = slaOptions.Value;
        _slaRealtimeSender = slaRealtimeSender;
    }


    public async Task<List<SlaTimelineDto>> GetTimelineAsync(
        Guid feedbackId,
        Guid actorUserId)
    {
        await EnsureFeedbackReadAccessAsync(feedbackId, actorUserId);

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



        var items = await _unitOfWork
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
                        SlaDateTimeHelper.AsUtc(
                            x.CreatedAt)
                })
            .ToListAsync();

        foreach (var item in items)
        {
            item.CreatedAt =
                SlaDateTimeHelper.AsUtc(
                    item.CreatedAt);
        }

        return items;
    }

    public async Task<SlaStatusDto> GetStatusAsync(
        Guid feedbackId,
        Guid actorUserId)
    {
        await EnsureFeedbackReadAccessAsync(feedbackId, actorUserId);

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

        var now =
            SlaDateTimeHelper.UtcNow;

        var startedAt =
            SlaDateTimeHelper.AsUtc(
                sla.StartedAt);

        var responseDueAt =
            SlaDateTimeHelper.AsUtc(
                sla.ResponseDueAt);

        var resolutionDueAt =
            SlaDateTimeHelper.AsUtc(
                sla.ResolutionDueAt);


        // =====================================================
        // XÁC ĐỊNH THỜI ĐIỂM DÙNG ĐỂ TÍNH SLA
        //
        // Running:
        //     dùng thời gian hiện tại.
        //
        // Paused:
        //     đóng băng tại PausedAt.
        // =====================================================

        var calculationTime = now;

        if (sla.Status == SlaStatus.Paused)
        {
            var openPause = await _unitOfWork
                .GetRepository<SlaPauseHistory>()
                .Entities
                .AsNoTracking()
                .Where(x =>
                    x.FeedbackSlaId ==
                    sla.FeedbackSlaId &&
                    !x.ResumedAt.HasValue)
                .OrderByDescending(x =>
                    x.PausedAt)
                .FirstOrDefaultAsync();

            if (openPause != null)
            {
                calculationTime =
                    SlaDateTimeHelper.AsUtc(
                        openPause.PausedAt);
            }
        }


        // =====================================================
        // TỔNG THỜI GIAN PAUSE ĐÃ HOÀN TẤT - CHÍNH XÁC
        //
        // Không dùng TotalPausedMinutes để tính nghiệp vụ vì
        // field đó chỉ dùng cho hiển thị theo phút và có thể bị
        // làm tròn. Deadline luôn được cộng đúng TimeSpan pause.
        // =====================================================

        var completedPausedDuration =
            await GetCompletedPausedDurationAsync(
                sla.FeedbackSlaId);

        var completedPausedMinutes =
            completedPausedDuration.TotalMinutes;


        // =====================================================
        // TỔNG THỜI GIAN SLA ACTIVE
        // =====================================================

        var responseTotal =
            Math.Max(
                0,
                (responseDueAt - startedAt)
                .TotalMinutes
                - completedPausedMinutes);

        var resolutionTotal =
            Math.Max(
                0,
                (resolutionDueAt - startedAt)
                .TotalMinutes
                - completedPausedMinutes);


        // =====================================================
        // THỜI GIAN ĐÃ SỬ DỤNG
        // =====================================================

        var responseUsed =
            Math.Max(
                0,
                (calculationTime - startedAt)
                .TotalMinutes
                - completedPausedMinutes);

        var resolutionUsed =
            Math.Max(
                0,
                (calculationTime - startedAt)
                .TotalMinutes
                - completedPausedMinutes);


        // =====================================================
        // REMAINING
        //
        // Khi Paused:
        // calculationTime = PausedAt
        // => remaining đứng im.
        //
        // Khi Running:
        // calculationTime = now
        // => remaining tiếp tục giảm.
        // =====================================================

        var responseRemainingMinutes =
            Math.Max(
                0,
                (responseDueAt - calculationTime)
                .TotalMinutes);

        var resolutionRemainingMinutes =
            Math.Max(
                0,
                (resolutionDueAt - calculationTime)
                .TotalMinutes);



        var responseRemainingSeconds =
            Math.Max(
                0,
                (int)Math.Floor(
                    (responseDueAt - calculationTime)
                    .TotalSeconds));

        var resolutionRemainingSeconds =
            Math.Max(
                0,
                (int)Math.Floor(
                    (resolutionDueAt - calculationTime)
                    .TotalSeconds));

        // =====================================================
        // WARNING
        //
        // Warning KHÔNG phải SlaTargetStatus.
        //
        // Target status chỉ gồm:
        // Pending / Met / Breached.
        //
        // Warning được lưu dưới dạng SlaEvent.
        // =====================================================

        var warningEvents = await _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.FeedbackSlaId ==
                    sla.FeedbackSlaId &&
                (
                    x.EventType ==
                        SlaEventType.ResponseWarning ||
                    x.EventType ==
                        SlaEventType.ResolutionWarning
                ))
            .Select(x =>
                x.EventType)
            .ToListAsync();

        var isResponseWarning =
            warningEvents.Contains(
                SlaEventType.ResponseWarning);

        var isResolutionWarning =
            warningEvents.Contains(
                SlaEventType.ResolutionWarning);


        return new SlaStatusDto
        {
            FeedbackId =
                sla.FeedbackId,

            FeedbackSlaId =
                sla.FeedbackSlaId,

            Status =
                sla.Status,

            ResponseStatus =
                sla.ResponseStatus,

            ResolutionStatus =
                sla.ResolutionStatus,


            ServerTime =
                now,

            StartedAt =
                startedAt,

            ResponseDueAt =
                responseDueAt,

            ResolutionDueAt =
                resolutionDueAt,


            ResponseRemainingMinutes =
                (int)Math.Floor(
                    responseRemainingMinutes),

            ResolutionRemainingMinutes =
                (int)Math.Floor(
                    resolutionRemainingMinutes),



            ResponseRemainingSeconds =
                responseRemainingSeconds,

            ResolutionRemainingSeconds =
                resolutionRemainingSeconds,

            ResponseProgressPercent =
                CalculatePercent(
                    responseUsed,
                    responseTotal),

            ResolutionProgressPercent =
                CalculatePercent(
                    resolutionUsed,
                    resolutionTotal),


            IsResponseWarning =
                isResponseWarning,

            IsResolutionWarning =
                isResolutionWarning,


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

        var incident = await ManagementAccessRules.EnsureManagerFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
            startedByUserId);
        if (incident.Status != IncidentStatus.Verified)
        {
            throw new InvalidOperationException(
                "Chỉ được bắt đầu SLA khi sự vụ đã được xác minh.");
        }

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

        var now = SlaDateTimeHelper.UtcNow;

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


        await SendSlaRealtimeSafeAsync(
            feedbackId,
            createdFeedbackSlaId,
            SlaEventType.Started);

        return await GetByIdAsync(
            createdFeedbackSlaId);
    }

    public async Task<FeedbackSlaDto>
        GetCurrentByFeedbackIdAsync(
            Guid feedbackId,
            Guid actorUserId)
    {
        ValidateFeedbackId(feedbackId);
        await EnsureFeedbackReadAccessAsync(feedbackId, actorUserId);

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

    private async Task<FeedbackSlaDto> GetByIdAsync(
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

        var incident = await ManagementAccessRules.EnsureStaffFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
            triggeredByUserId);
        if (incident.Status != IncidentStatus.InProgress)
        {
            throw new InvalidOperationException(
                "Chỉ ghi nhận phản hồi SLA khi sự vụ đang được xử lý.");
        }

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

        var now = SlaDateTimeHelper.UtcNow;

        entity.RespondedAt = now;

        entity.ResponseStatus =
            now <= SlaDateTimeHelper.AsUtc(
                entity.ResponseDueAt)
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

        await SendSlaRealtimeSafeAsync(
            entity.FeedbackId,
            entity.FeedbackSlaId,
            SlaEventType.Responded);


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

        await ManagementAccessRules.EnsureManagerFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
            pausedByUserId);

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

        var now = SlaDateTimeHelper.UtcNow;
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

        await SendSlaRealtimeSafeAsync(
            entity.FeedbackId,
            entity.FeedbackSlaId,
            SlaEventType.Paused);


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

        await ManagementAccessRules.EnsureManagerFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
            resumedByUserId);

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

        var now = SlaDateTimeHelper.UtcNow;

        var pausedAt =
            SlaDateTimeHelper.AsUtc(
                pauseHistory.PausedAt);

        var pauseDuration =
            now - pausedAt;

        if (pauseDuration < TimeSpan.Zero)
        {
            pauseDuration = TimeSpan.Zero;
        }

        /*
         * PausedMinutes chỉ phục vụ hiển thị/lịch sử.
         * Không dùng giá trị đã làm tròn này để dời deadline.
         */
        var pausedMinutesForDisplay =
            (int)Math.Floor(
                pauseDuration.TotalMinutes);

        pauseHistory.ResumedAt = now;
        pauseHistory.PausedMinutes =
            pausedMinutesForDisplay;

        pauseHistory.ResumedByUserId =
            resumedByUserId;

        pauseHistory.UpdatedAt = now;

        var oldStatus = entity.Status;

        entity.Status = SlaStatus.Running;

        /*
         * Deadline phải cộng chính xác toàn bộ TimeSpan pause
         * (đến giây/millisecond), không cộng số phút đã làm tròn.
         */
        if (!entity.RespondedAt.HasValue)
        {
            entity.ResponseDueAt =
                SlaDateTimeHelper.AsUtc(
                    entity.ResponseDueAt)
                .Add(pauseDuration);
        }

        if (!entity.ResolvedAt.HasValue)
        {
            entity.ResolutionDueAt =
                SlaDateTimeHelper.AsUtc(
                    entity.ResolutionDueAt)
                .Add(pauseDuration);
        }

        /*
         * TotalPausedMinutes chỉ là số phút hiển thị.
         * Tính lại từ tổng thời lượng pause thực tế để tránh
         * cộng dồn sai số qua nhiều lần pause/resume.
         */
        var previousPausedDuration =
            await GetCompletedPausedDurationAsync(
                entity.FeedbackSlaId);

        var totalPausedDuration =
            previousPausedDuration +
            pauseDuration;

        entity.TotalPausedMinutes =
            (int)Math.Floor(
                totalPausedDuration.TotalMinutes);

        entity.UpdatedAt = now;

        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Resumed,
            oldStatus,
            SlaStatus.Running,
            request.Note ??
            $"Tiếp tục SLA sau {FormatPauseDuration(pauseDuration)} tạm dừng.",
            resumedByUserId,
            SlaTriggerSource.Manager);

        await _unitOfWork.SaveAsync();

        await SendSlaRealtimeSafeAsync(
            entity.FeedbackId,
            entity.FeedbackSlaId,
            SlaEventType.Resumed);


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

        var incident = await ManagementAccessRules.EnsureManagerFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
            completedByUserId);
        if (incident.Status != IncidentStatus.Approved)
        {
            throw new InvalidOperationException(
                "Chỉ hoàn thành SLA sau khi Manager đã duyệt kết quả sự vụ.");
        }

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

        var now = SlaDateTimeHelper.UtcNow;
        var oldStatus = entity.Status;

        entity.ResolvedAt = now;

        entity.CompletedByUserId =
            completedByUserId;

        entity.Status = SlaStatus.Completed;

        entity.ResolutionStatus =
            now <= SlaDateTimeHelper.AsUtc(
                entity.ResolutionDueAt)
                ? SlaTargetStatus.Met
                : SlaTargetStatus.Breached;

        entity.IsResolutionBreached =
            entity.ResolutionStatus ==
            SlaTargetStatus.Breached;

        if (!entity.RespondedAt.HasValue)
        {
            entity.RespondedAt = now;

            entity.ResponseStatus =
                now <= SlaDateTimeHelper.AsUtc(
                    entity.ResponseDueAt)
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

        await SendSlaRealtimeSafeAsync(
            entity.FeedbackId,
            entity.FeedbackSlaId,
            SlaEventType.Completed);


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

        var incident = await ManagementAccessRules.EnsureManagerFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
            cancelledByUserId);
        if (incident.Status != IncidentStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Chỉ hủy SLA sau khi sự vụ đã được hủy.");
        }

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

        var now = SlaDateTimeHelper.UtcNow;
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

        await SendSlaRealtimeSafeAsync(
            entity.FeedbackId,
            entity.FeedbackSlaId,
            SlaEventType.Cancelled);


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

        await ManagementAccessRules.EnsureManagerFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
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
                SlaDateTimeHelper.UtcNow);



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
         * Tính lại deadline.
         *
         * Dùng tổng pause chính xác từ lịch sử thay vì
         * TotalPausedMinutes vì field này chỉ là số phút hiển thị.
         */
        var completedPausedDuration =
            await GetCompletedPausedDurationAsync(
                entity.FeedbackSlaId);

        var startedAtUtc =
            SlaDateTimeHelper.AsUtc(
                entity.StartedAt);

        entity.ResponseDueAt =
            startedAtUtc
                .AddMinutes(
                    policy.ResponseTimeMinutes)
                .Add(
                    completedPausedDuration);



        entity.ResolutionDueAt =
            startedAtUtc
                .AddMinutes(
                    policy.ResolutionTimeMinutes)
                .Add(
                    completedPausedDuration);



        var now =
            SlaDateTimeHelper.UtcNow;



        /*
         * Update Response SLA status
         */
        if (!entity.RespondedAt.HasValue)
        {
            entity.ResponseStatus =
                now > SlaDateTimeHelper.AsUtc(
                    entity.ResponseDueAt)
                    ? SlaTargetStatus.Breached
                    : SlaTargetStatus.Pending;
        }
        else
        {
            entity.ResponseStatus =
                SlaDateTimeHelper.AsUtc(
                    entity.RespondedAt.Value) <=
                SlaDateTimeHelper.AsUtc(
                    entity.ResponseDueAt)
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
                now > SlaDateTimeHelper.AsUtc(
                    entity.ResolutionDueAt)
                    ? SlaTargetStatus.Breached
                    : SlaTargetStatus.Pending;
        }
        else
        {
            entity.ResolutionStatus =
                SlaDateTimeHelper.AsUtc(
                    entity.ResolvedAt.Value) <=
                SlaDateTimeHelper.AsUtc(
                    entity.ResolutionDueAt)
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

        await SendSlaRealtimeSafeAsync(
            entity.FeedbackId,
            entity.FeedbackSlaId,
            SlaEventType.Recalculated);




        return await GetByIdAsync(
            entity.FeedbackSlaId);
    }

    public async Task CheckViolationAsync(
        long feedbackSlaId,
        Guid actorUserId)
    {
        ValidateFeedbackSlaId(feedbackSlaId);

        var feedbackId = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .Where(sla => sla.FeedbackSlaId == feedbackSlaId)
            .Select(sla => (Guid?)sla.FeedbackId)
            .SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy SLA.");
        await ManagementAccessRules.EnsureManagerFeedbackOperationAsync(
            _unitOfWork,
            feedbackId,
            actorUserId);

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

        await SendRealtimeMonitoringEventsAsync(
            entity,
            result);


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

                await SendRealtimeMonitoringEventsAsync(
                    entity,
                    result);


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
        var now = SlaDateTimeHelper.UtcNow;

        var result =
            new SlaMonitoringCheckResult();

        var thresholdPercent = Math.Clamp(
            _slaOptions.WarningThresholdPercent,
            1,
            99);

        /*
         * Tổng thời lượng pause đã hoàn tất dùng cho tính tỷ lệ SLA.
         * Không dùng TotalPausedMinutes để tránh sai số làm tròn.
         */
        var completedPausedDuration =
            await GetCompletedPausedDurationAsync(
                entity.FeedbackSlaId);

        var completedPausedMinutes =
            completedPausedDuration.TotalMinutes;

        /*
         * RESPONSE WARNING
         *
         * Cảnh báo khi phần thời gian SLA còn lại <= threshold.
         */
        if (!entity.RespondedAt.HasValue &&
            !entity.IsResponseBreached &&
            now <= SlaDateTimeHelper.AsUtc(
                entity.ResponseDueAt))
        {
            var totalResponseMinutes =
                Math.Max(
                    0,
                    (
                        SlaDateTimeHelper.AsUtc(
                            entity.ResponseDueAt) -
                        SlaDateTimeHelper.AsUtc(
                            entity.StartedAt)
                    )
                    .TotalMinutes
                    - completedPausedMinutes);

            var remainingResponseMinutes =
                Math.Max(
                    0,
                    (
                        SlaDateTimeHelper.AsUtc(
                            entity.ResponseDueAt) -
                        now
                    )
                    .TotalMinutes);

            var remainingResponsePercent =
                totalResponseMinutes <= 0
                    ? 0
                    : remainingResponseMinutes /
                      totalResponseMinutes *
                      100;

            var hasResponseWarning =
                await HasSlaEventAsync(
                    entity.FeedbackSlaId,
                    SlaEventType.ResponseWarning);

            if (remainingResponsePercent <= thresholdPercent &&
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

        /*
         * RESOLUTION WARNING
         */
        if (!entity.ResolvedAt.HasValue &&
            !entity.IsResolutionBreached &&
            now <= SlaDateTimeHelper.AsUtc(
                entity.ResolutionDueAt))
        {
            var totalResolutionMinutes =
                Math.Max(
                    0,
                    (
                        SlaDateTimeHelper.AsUtc(
                            entity.ResolutionDueAt) -
                        SlaDateTimeHelper.AsUtc(
                            entity.StartedAt)
                    )
                    .TotalMinutes
                    - completedPausedMinutes);

            var remainingResolutionMinutes =
                Math.Max(
                    0,
                    (
                        SlaDateTimeHelper.AsUtc(
                            entity.ResolutionDueAt) -
                        now
                    )
                    .TotalMinutes);

            var remainingResolutionPercent =
                totalResolutionMinutes <= 0
                    ? 0
                    : remainingResolutionMinutes /
                      totalResolutionMinutes *
                      100;

            var hasResolutionWarning =
                await HasSlaEventAsync(
                    entity.FeedbackSlaId,
                    SlaEventType.ResolutionWarning);

            if (remainingResolutionPercent <= thresholdPercent &&
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

        /*
         * RESPONSE BREACH
         */
        if (!entity.RespondedAt.HasValue &&
            !entity.IsResponseBreached &&
            now > SlaDateTimeHelper.AsUtc(
                entity.ResponseDueAt))
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
            now > SlaDateTimeHelper.AsUtc(
                entity.ResolutionDueAt))
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
        /*
         * Provider Coordinator:
         * vẫn giữ nguyên email warning/breach như logic hiện tại.
         *
         * InteractionManager + SystemAdmin:
         * nhận in-app/realtime notification qua NotificationService.
         */
        if (result.ResponseWarningCreated)
        {
            await SendWarningEmailToProviderAsync(
                entity,
                SlaEventType.ResponseWarning);

            await SendInternalSlaNotificationAsync(
                entity,
                SlaEventType.ResponseWarning);
        }

        if (result.ResolutionWarningCreated)
        {
            await SendWarningEmailToProviderAsync(
                entity,
                SlaEventType.ResolutionWarning);

            await SendInternalSlaNotificationAsync(
                entity,
                SlaEventType.ResolutionWarning);
        }

        await SendBreachNotificationsSafeAsync(
            entity,
            result.ResponseJustBreached,
            result.ResolutionJustBreached);

        if (result.ResponseJustBreached)
        {
            await SendInternalSlaNotificationAsync(
                entity,
                SlaEventType.ResponseBreached);
        }

        if (result.ResolutionJustBreached)
        {
            await SendInternalSlaNotificationAsync(
                entity,
                SlaEventType.ResolutionBreached);
        }
    }

    private async Task SendInternalSlaNotificationAsync(
        FeedbackSla entity,
        string eventType)
    {
        /*
         * Theo database hiện tại:
         * - InteractionManager = Manager
         * - SystemAdmin = Admin
         *
         * Chỉ lấy account đang active.
         */
        var targetRoleIds = await _unitOfWork
            .GetRepository<Role>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.RoleName.ToUpper() == UserRole.INTERACTIONMANAGER ||
                x.RoleName.ToUpper() == UserRole.SYSTEMADMIN)
            .Select(x => x.RoleId)
            .ToListAsync();

        if (targetRoleIds.Count == 0)
        {
            _logger.LogWarning(
                "Không tìm thấy role InteractionManager/SystemAdmin để gửi SLA notification.");

            return;
        }

        var recipients = await _unitOfWork
            .GetRepository<User>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                targetRoleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .ToListAsync();

        if (recipients.Count == 0)
        {
            _logger.LogDebug(
                "Không có InteractionManager/SystemAdmin đang active để nhận SLA notification.");

            return;
        }

        var isWarning =
            eventType == SlaEventType.ResponseWarning ||
            eventType == SlaEventType.ResolutionWarning;

        var isResponse =
            eventType == SlaEventType.ResponseWarning ||
            eventType == SlaEventType.ResponseBreached;

        var deadlineUtc =
            isResponse
                ? entity.ResponseDueAt
                : entity.ResolutionDueAt;

        var targetLabel =
            isResponse
                ? "phản hồi đầu tiên"
                : "hoàn thành xử lý";

        var deadlineDisplay =
            SlaDateTimeHelper.FormatVietnamDateTime(
                deadlineUtc);

        var title =
            isWarning
                ? $"Cảnh báo SLA {targetLabel}"
                : $"Vi phạm SLA {targetLabel}";

        var message =
            isWarning
                ? $"Feedback \"{entity.Feedback?.Title ?? entity.FeedbackId.ToString()}\" " +
                  $"chỉ còn khoảng {Math.Clamp(_slaOptions.WarningThresholdPercent, 1, 99)}% " +
                  $"thời gian SLA {targetLabel}. Hạn: {deadlineDisplay}."
                : $"Feedback \"{entity.Feedback?.Title ?? entity.FeedbackId.ToString()}\" " +
                  $"đã vi phạm thời hạn SLA {targetLabel}. Hạn: {deadlineDisplay}.";

        foreach (var userId in recipients.Distinct())
        {
            try
            {
                await _notificationService.SendAsync(
                    userId,
                    title,
                    message,
                    NotificationType.TicketUpdated,
                    $"/feedbacks/{entity.FeedbackId}");
            }
            catch (Exception ex)
            {
                /*
                 * Một user nhận notification lỗi không được làm
                 * background SLA dừng hoặc chặn các user còn lại.
                 */
                _logger.LogError(
                    ex,
                    "Không thể gửi SLA notification {EventType} đến user {UserId} cho feedback {FeedbackId}.",
                    eventType,
                    userId,
                    entity.FeedbackId);
            }
        }
    }

    private async Task SendSlaRealtimeSafeAsync(
        Guid feedbackId,
        long feedbackSlaId,
        string eventType)
    {
        try
        {
            await _slaRealtimeSender.SendSlaUpdatedAsync(
                feedbackId,
                feedbackSlaId,
                eventType);
        }
        catch (Exception ex)
        {
            /*
             * SignalR lỗi không được làm thất bại nghiệp vụ SLA
             * sau khi dữ liệu đã được lưu thành công.
             */
            _logger.LogError(
                ex,
                "Không thể gửi SignalR SLA event {EventType}. " +
                "FeedbackId: {FeedbackId}, FeedbackSlaId: {FeedbackSlaId}.",
                eventType,
                feedbackId,
                feedbackSlaId);
        }
    }

    private async Task SendRealtimeMonitoringEventsAsync(
        FeedbackSla entity,
        SlaMonitoringCheckResult result)
    {
        if (result.ResponseWarningCreated)
        {
            await SendSlaRealtimeSafeAsync(
                entity.FeedbackId,
                entity.FeedbackSlaId,
                SlaEventType.ResponseWarning);
        }

        if (result.ResolutionWarningCreated)
        {
            await SendSlaRealtimeSafeAsync(
                entity.FeedbackId,
                entity.FeedbackSlaId,
                SlaEventType.ResolutionWarning);
        }

        if (result.ResponseJustBreached)
        {
            await SendSlaRealtimeSafeAsync(
                entity.FeedbackId,
                entity.FeedbackSlaId,
                SlaEventType.ResponseBreached);
        }

        if (result.ResolutionJustBreached)
        {
            await SendSlaRealtimeSafeAsync(
                entity.FeedbackId,
                entity.FeedbackSlaId,
                SlaEventType.ResolutionBreached);
        }
    }

    private async Task<TimeSpan>
        GetCompletedPausedDurationAsync(
            long feedbackSlaId)
    {
        var pauseItems = await _unitOfWork
            .GetRepository<SlaPauseHistory>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.FeedbackSlaId == feedbackSlaId &&
                x.ResumedAt.HasValue)
            .Select(x => new
            {
                x.PausedAt,
                x.ResumedAt
            })
            .ToListAsync();

        var totalTicks = pauseItems
            .Select(x =>
            {
                var pausedAt =
                    SlaDateTimeHelper.AsUtc(
                        x.PausedAt);

                var resumedAt =
                    SlaDateTimeHelper.AsUtc(
                        x.ResumedAt!.Value);

                var duration =
                    resumedAt - pausedAt;

                return duration > TimeSpan.Zero
                    ? duration.Ticks
                    : 0L;
            })
            .Sum();

        return TimeSpan.FromTicks(
            totalTicks);
    }

    private static string FormatPauseDuration(
        TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var totalSeconds =
            (int)Math.Floor(
                duration.TotalSeconds);

        if (totalSeconds < 60)
        {
            return $"{totalSeconds} giây";
        }

        var minutes =
            totalSeconds / 60;

        var seconds =
            totalSeconds % 60;

        if (seconds == 0)
        {
            return $"{minutes} phút";
        }

        return $"{minutes} phút {seconds} giây";
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
                    SlaDateTimeHelper.FormatVietnamDateTime(
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
                    SlaDateTimeHelper.FormatVietnamDateTime(
                        deadlineUtc),
                breachedAtDisplay:
                    SlaDateTimeHelper.FormatVietnamDateTime(
                        SlaDateTimeHelper.UtcNow));

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

    private async Task EnsureFeedbackReadAccessAsync(
        Guid feedbackId,
        Guid actorUserId)
    {
        ValidateFeedbackId(feedbackId);
        ValidateUserId(actorUserId);

        var actorRole = await _unitOfWork
            .GetRepository<User>()
            .Entities
            .AsNoTracking()
            .Where(user => user.UserId == actorUserId && user.IsActive)
            .Select(user => user.Role.RoleName)
            .SingleOrDefaultAsync()
            ?? throw new UnauthorizedAccessException(
                "Không tìm thấy người dùng hoặc tài khoản đã bị khóa.");

        if (actorRole.ToUpperInvariant() == UserRole.SERVICEUSER)
        {
            var isOwner = await _unitOfWork
                .GetRepository<Feedback>()
                .Entities
                .AsNoTracking()
                .AnyAsync(feedback =>
                    feedback.FeedbackId == feedbackId &&
                    feedback.UserId == actorUserId);
            if (!isOwner)
            {
                throw new ForbiddenAccessException(
                    "Bạn không có quyền xem SLA của phản ánh này.");
            }

            return;
        }

        await ManagementAccessRules.EnsureFeedbackReadAccessAsync(
            _unitOfWork,
            feedbackId,
            actorUserId);
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
            CreatedAt = SlaDateTimeHelper.UtcNow
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
        var now = SlaDateTimeHelper.UtcNow;

        double? remainingResponseMinutes = null;
        double? remainingResolutionMinutes = null;

        if (!entity.RespondedAt.HasValue &&
            entity.Status == SlaStatus.Running)
        {
            remainingResponseMinutes =
                Math.Round(
                    (SlaDateTimeHelper.AsUtc(
                        entity.ResponseDueAt) - now)
                    .TotalMinutes,
                    2);
        }

        if (!entity.ResolvedAt.HasValue &&
            entity.Status == SlaStatus.Running)
        {
            remainingResolutionMinutes =
                Math.Round(
                    (SlaDateTimeHelper.AsUtc(
                        entity.ResolutionDueAt) - now)
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
                SlaDateTimeHelper.AsUtc(
                    entity.StartedAt),

            ResponseDueAt =
                SlaDateTimeHelper.AsUtc(
                    entity.ResponseDueAt),

            ResolutionDueAt =
                SlaDateTimeHelper.AsUtc(
                    entity.ResolutionDueAt),

            RespondedAt =
                SlaDateTimeHelper.AsUtc(
                    entity.RespondedAt),

            ResolvedAt =
                SlaDateTimeHelper.AsUtc(
                    entity.ResolvedAt),

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
                SlaDateTimeHelper.AsUtc(
                    entity.CreatedAt),

            UpdatedAt =
                SlaDateTimeHelper.AsUtc(
                    entity.UpdatedAt),

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
            SlaDateTimeHelper.AsUtc(
                x.CreatedAt)
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
                                SlaDateTimeHelper.AsUtc(
                                    x.PausedAt),

                            ResumedAt =
                                SlaDateTimeHelper.AsUtc(
                                    x.ResumedAt),

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
                                SlaDateTimeHelper.AsUtc(
                                    x.CreatedAt),

                            UpdatedAt =
                                SlaDateTimeHelper.AsUtc(
                                    x.UpdatedAt)
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
