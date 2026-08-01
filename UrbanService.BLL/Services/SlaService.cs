using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.SLA;
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

    public SlaService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    ILogger<SlaService> logger,
    IOptions<SlaMonitoringOptions> slaOptions)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
        _slaOptions = slaOptions.Value;
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
                triggerSource: SlaTriggerSource.User);

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
            SlaTriggerSource.User);

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
            SlaTriggerSource.User);

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
            SlaTriggerSource.User);

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
            SlaTriggerSource.User);

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
            SlaTriggerSource.User);

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

        var policy = await FindApplicablePolicyAsync(
            entity.AreaId,
            entity.CategoryId,
            entity.Priority,
            DateTime.UtcNow);

        var oldPolicyId = entity.SlaPolicyId;
        var oldResponseDueAt =
            entity.ResponseDueAt;

        var oldResolutionDueAt =
            entity.ResolutionDueAt;

        entity.SlaPolicyId =
            policy.SlaPolicyId;

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

        var now = DateTime.UtcNow;

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
                entity.RespondedAt.Value <=
                entity.ResponseDueAt
                    ? SlaTargetStatus.Met
                    : SlaTargetStatus.Breached;
        }

        entity.IsResponseBreached =
            entity.ResponseStatus ==
            SlaTargetStatus.Breached;

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
                entity.ResolvedAt.Value <=
                entity.ResolutionDueAt
                    ? SlaTargetStatus.Met
                    : SlaTargetStatus.Breached;
        }

        entity.IsResolutionBreached =
            entity.ResolutionStatus ==
            SlaTargetStatus.Breached;

        entity.UpdatedAt = now;

        var eventNote =
            request.Note ??
            $"Tính lại SLA. Policy: {oldPolicyId} → " +
            $"{policy.SlaPolicyId}. " +
            $"ResponseDueAt: {oldResponseDueAt:O} → " +
            $"{entity.ResponseDueAt:O}. " +
            $"ResolutionDueAt: {oldResolutionDueAt:O} → " +
            $"{entity.ResolutionDueAt:O}.";

        await AddEventAsync(
            entity.FeedbackSlaId,
            SlaEventType.Recalculated,
            entity.Status,
            entity.Status,
            eventNote,
            recalculatedByUserId,
            SlaTriggerSource.User);

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
            .Include(x => x.Feedback)
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

        var wasResponseBreached =
            entity.IsResponseBreached;

        var wasResolutionBreached =
            entity.IsResolutionBreached;

        var hasChanged =
            await ApplyViolationCheckAsync(entity);

        if (!hasChanged)
        {
            return;
        }

        await _unitOfWork.SaveAsync();

        await SendBreachNotificationsSafeAsync(
            entity,
            !wasResponseBreached &&
            entity.IsResponseBreached,
            !wasResolutionBreached &&
            entity.IsResolutionBreached);
    }

    public async Task<int> CheckAllRunningSlasAsync()
    {
        var runningSlas = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .Include(x => x.Feedback)
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
            var wasResponseBreached =
                entity.IsResponseBreached;

            var wasResolutionBreached =
                entity.IsResolutionBreached;

            try
            {
                var hasChanged =
                    await ApplyViolationCheckAsync(entity);

                if (!hasChanged)
                {
                    continue;
                }

                /*
                 * Lưu SLA và event của SLA hiện tại trước.
                 */
                await _unitOfWork.SaveAsync();

                updatedCount++;

                await SendBreachNotificationsSafeAsync(
                    entity,
                    !wasResponseBreached &&
                    entity.IsResponseBreached,
                    !wasResolutionBreached &&
                    entity.IsResolutionBreached);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Không thể kiểm tra SLA {FeedbackSlaId}.",
                    entity.FeedbackSlaId);

                /*
                 * Không throw để worker vẫn kiểm tra SLA tiếp theo.
                 *
                 * Tuy nhiên nếu UnitOfWork của bạn có ChangeTracker
                 * thì phương án tốt nhất về sau là tạo scope riêng
                 * cho từng SLA.
                 */
            }
        }

        _logger.LogInformation(
            "Đã cập nhật {UpdatedCount} SLA vi phạm.",
            updatedCount);

        return updatedCount;
    }

    private async Task<bool> ApplyViolationCheckAsync(
    FeedbackSla entity)
    {
        var now = DateTime.UtcNow;
        var hasChanged = false;

        var warningChanged =
        await CheckWarningAsync(entity);


        if (warningChanged)
        {
            hasChanged = true;
        }

        /*
         * Quá hạn phản hồi đầu tiên.
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

            hasChanged = true;

            _logger.LogWarning(
                "SLA {FeedbackSlaId} của feedback {FeedbackId} đã quá hạn phản hồi.",
                entity.FeedbackSlaId,
                entity.FeedbackId);
        }

        /*
         * Quá hạn hoàn thành xử lý.
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

            hasChanged = true;

            _logger.LogWarning(
                "SLA {FeedbackSlaId} của feedback {FeedbackId} đã quá hạn xử lý.",
                entity.FeedbackSlaId,
                entity.FeedbackId);
        }

        return hasChanged;
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
                "Không thể gửi thông báo cho SLA {FeedbackSlaId} vì chưa load Feedback.",
                entity.FeedbackSlaId);

            return;
        }

        if (entity.Feedback.UserId == Guid.Empty)
        {
            _logger.LogWarning(
                "Không thể gửi thông báo cho SLA {FeedbackSlaId} vì UserId không hợp lệ.",
                entity.FeedbackSlaId);

            return;
        }

        var feedbackTitle =
            string.IsNullOrWhiteSpace(entity.Feedback.Title)
                ? "Phản ánh của bạn"
                : entity.Feedback.Title;

        var targetUrl =
            $"/feedbacks/{entity.FeedbackId}";

        if (responseJustBreached)
        {
            try
            {
                await _notificationService.SendAsync(
                    entity.Feedback.UserId,
                    "Phản ánh đang chậm phản hồi",
                    $"Phản ánh \"{feedbackTitle}\" đã vượt quá thời hạn phản hồi dự kiến. " +
                    "Hệ thống đã ghi nhận và đang tiếp tục theo dõi.",
                    SlaEventType.ResponseBreached,
                    targetUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Không thể gửi thông báo quá hạn phản hồi cho SLA {FeedbackSlaId}.",
                    entity.FeedbackSlaId);
            }
        }

        if (resolutionJustBreached)
        {
            try
            {
                await _notificationService.SendAsync(
                    entity.Feedback.UserId,
                    "Phản ánh đang chậm xử lý",
                    $"Phản ánh \"{feedbackTitle}\" đã vượt quá thời hạn hoàn thành xử lý dự kiến. " +
                    "Hệ thống đã ghi nhận và đang tiếp tục theo dõi.",
                    SlaEventType.ResolutionBreached,
                    targetUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Không thể gửi thông báo quá hạn xử lý cho SLA {FeedbackSlaId}.",
                    entity.FeedbackSlaId);
            }
        }
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

    private async Task<bool> CheckWarningAsync(
    FeedbackSla entity)
    {
        var now = DateTime.UtcNow;

        var changed = false;


        /*
         * Response warning
         */

        if (!entity.RespondedAt.HasValue)
        {
            var warningTime =
                entity.StartedAt.AddMinutes(
                    (entity.ResponseDueAt - entity.StartedAt)
                    .TotalMinutes *
                    (100 - _slaOptions.WarningThresholdPercent)
                    / 100);


            if (now >= warningTime &&
                !HasWarningEvent(
                    entity.FeedbackSlaId,
                    SlaEventType.Warning))
            {
                await AddEventAsync(
                    entity.FeedbackSlaId,
                    SlaEventType.Warning,
                    entity.Status,
                    entity.Status,
                    "Sắp hết hạn phản hồi SLA.",
                    null,
                    SlaTriggerSource.System);


                await SendWarningNotificationAsync(
                    entity,
                    "Phản ánh sắp hết hạn phản hồi.");

                changed = true;
            }
        }




        /*
         * Resolution warning
         */

        if (!entity.ResolvedAt.HasValue)
        {
            var warningTime =
                entity.StartedAt.AddMinutes(
                    (entity.ResolutionDueAt - entity.StartedAt)
                    .TotalMinutes *
                    (100 - _slaOptions.WarningThresholdPercent)
                    / 100);


            if (now >= warningTime &&
                !HasWarningEvent(
                    entity.FeedbackSlaId,
                    SlaEventType.Warning))
            {

                await AddEventAsync(
                    entity.FeedbackSlaId,
                    SlaEventType.Warning,
                    entity.Status,
                    entity.Status,
                    "Sắp hết hạn xử lý SLA.",
                    null,
                    SlaTriggerSource.System);


                await SendWarningNotificationAsync(
                    entity,
                    "Phản ánh sắp hết hạn xử lý.");


                changed = true;
            }
        }


        return changed;
    }

    private async Task SendWarningNotificationAsync(
    FeedbackSla entity,
    string message)
    {
        if (entity.Feedback == null)
            return;


        try
        {
            await _notificationService.SendAsync(
                entity.Feedback.UserId,

                "Cảnh báo SLA",

                message +
                $" Feedback: {entity.Feedback.Title}",

                SlaEventType.Warning,

                $"/feedbacks/{entity.FeedbackId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Không thể gửi SLA warning notification.");
        }
    }

    private bool HasWarningEvent(
    long feedbackSlaId,
    string eventType)
    {
        return _unitOfWork
            .GetRepository<SlaEvent>()
            .Entities
            .Any(x =>
                x.FeedbackSlaId == feedbackSlaId &&
                x.EventType == eventType);
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
                "Critical",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        throw new ArgumentException(
            "Priority không hợp lệ. " +
            "Chỉ chấp nhận Low, Medium, High hoặc Critical.");
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}