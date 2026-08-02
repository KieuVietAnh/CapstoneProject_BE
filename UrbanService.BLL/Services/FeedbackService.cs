using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using UrbanService.BLL.DTOs.SLA;



namespace UrbanService.BLL.Services;

public class FeedbackService : IFeedbackService
{
    private const int MaxPageSize = 100;
    private static readonly IReadOnlyCollection<string> AllowedProviderReportStatuses =
    [
        "Reported",
        "InProgress",
        "Done",
        "Failed",
        "Cancelled"
    ];

    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly IAiFeedbackReviewQueue _aiFeedbackReviewQueue;
    private readonly IAiFeedbackDuplicateService _aiFeedbackDuplicateService;
    private readonly ISlaService _slaService;


    public FeedbackService(
    IUnitOfWork uow,
    INotificationService notificationService,
    IAiFeedbackReviewQueue aiFeedbackReviewQueue,
    IAiFeedbackDuplicateService aiFeedbackDuplicateService,
    ISlaService slaService)
    {
        _uow = uow;
        _notificationService = notificationService;
        _aiFeedbackReviewQueue = aiFeedbackReviewQueue;
        _aiFeedbackDuplicateService = aiFeedbackDuplicateService;
        _slaService = slaService;
    }

    public async Task<FeedbackDetailDto> CreateAsync(
        Guid userId,
        FeedbackCreateRequest request,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments)
    {
        ValidateCreate(request);
        await EnsureAreaMatchesLocationAsync(request.AreaId, request.Latitude, request.Longitude);

        var now = DateTime.UtcNow;
        var feedback = new Feedback
        {
            FeedbackId = Guid.NewGuid(),
            UserId = userId,
            AreaId = request.AreaId,
            CategoryId = null,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            LocationText = request.LocationText.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationAccuracyMeters = request.LocationAccuracyMeters,
            GeoSource = NormalizeOptional(request.GeoSource),
            IsLocationVerified = false,
            Priority = null,
            Status = FeedbackStatus.Submitted,
            DueDate = request.DueDate,
            IsMasterTicket = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var attachment in attachments)
        {
            feedback.FeedbackAttachments.Add(new FeedbackAttachment
            {
                FileUrl = attachment.FileUrl,
                FileType = attachment.FileType,
                UploadedAt = now
            });
        }

        feedback.FeedbackStatusHistories.Add(new FeedbackStatusHistory
        {
            ChangedByUserId = userId,
            OldStatus = null,
            NewStatus = feedback.Status,
            Note = "Feedback created",
            ChangedAt = now
        });

        await _uow.GetRepository<Feedback>().AddAsync(feedback);
        await _uow.SaveAsync();

        await _aiFeedbackDuplicateService.CheckAndLinkDuplicateAsync(feedback, userId);
        await _aiFeedbackReviewQueue.EnqueueAsync(feedback.FeedbackId, userId);
        await SendFeedbackNotificationAsync(
            feedback,
            "Phản ánh đã được tạo",
            $"Phản ánh \"{feedback.Title}\" đã được tiếp nhận và đang chờ xử lý.");

        return await GetMyFeedbackDetailAsync(userId, feedback.FeedbackId);
    }

    public async Task<PagedResultDto<FeedbackListItemDto>> GetMyFeedbacksAsync(Guid userId, FeedbackQueryParameters query)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var search = query.Search?.Trim().ToLower();
        var status = query.Status?.Trim().ToLower();

        var feedbacks = _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Where(f => f.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            feedbacks = feedbacks.Where(f => f.Status.ToLower() == status);
        }

        if (query.CategoryId.HasValue)
        {
            feedbacks = feedbacks.Where(f => f.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            feedbacks = feedbacks.Where(f =>
                f.Title.ToLower().Contains(search) ||
                f.Description.ToLower().Contains(search) ||
                f.LocationText.ToLower().Contains(search));
        }

        var totalItems = await feedbacks.CountAsync();
        var items = await feedbacks
            .OrderByDescending(f => f.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FeedbackListItemDto
            {
                FeedbackId = f.FeedbackId,
                UserId = f.UserId,
                UserName = f.User.FullName,
                AreaId = f.AreaId,
                AreaName = f.Area.AreaName,
                CategoryId = f.CategoryId,
                CategoryName = f.Category.CategoryName,
                Title = f.Title,
                LocationText = f.LocationText,
                Priority = f.Priority,
                Status = f.Status,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt,
                AttachmentCount = f.FeedbackAttachments.Count,
                CommentCount = f.FeedbackComments.Count,
                SupportCount = f.FeedbackSupports.Count,
                DuplicateWarning = f.FeedbackDuplicateCandidates.Any(candidate => candidate.Status == "Pending"),
                ParentTicketId = f.ParentTicketId,
                IsMasterTicket = f.IsMasterTicket
            })
            .ToListAsync();

        return new PagedResultDto<FeedbackListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<FeedbackDetailDto> GetMyFeedbackDetailAsync(Guid userId, Guid feedbackId)
    {
        var feedback = await GetOwnedFeedbackWithDetailsAsync(userId, feedbackId, asNoTracking: true);
        var detail = MapDetail(feedback, userId);
        await PopulateDuplicateInfoAsync(detail);
        return detail;
    }

    public async Task<FeedbackDetailDto> GetResidentFeedFeedbackDetailAsync(Guid currentUserId, Guid feedbackId)
    {
        var feedback = await GetFeedbackWithDetailsAsync(feedbackId, asNoTracking: true);

        if (IsInternalFeedbackStatus(feedback.Status))
        {
            throw new Exception("Feedback này chưa được công khai trên bảng tin.");
        }

        var detail = MapDetail(feedback, currentUserId);
        await PopulateDuplicateInfoAsync(detail);
        return detail;
    }

    public async Task<PagedResultDto<FeedbackListItemDto>> GetResidentFeedFeedbacksAsync(FeedbackQueryParameters query)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var search = query.Search?.Trim().ToLower();
        var status = query.Status?.Trim().ToLower();

        var feedbacks = _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Where(f => !InternalFeedbackStatuses.Contains(f.Status));

        if (!string.IsNullOrWhiteSpace(status))
        {
            feedbacks = feedbacks.Where(f => f.Status.ToLower() == status);
        }

        if (query.CategoryId.HasValue)
        {
            feedbacks = feedbacks.Where(f => f.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            feedbacks = feedbacks.Where(f =>
                f.Title.ToLower().Contains(search) ||
                f.Description.ToLower().Contains(search) ||
                f.LocationText.ToLower().Contains(search));
        }

        var totalItems = await feedbacks.CountAsync();
        var items = await feedbacks
            .OrderByDescending(f => f.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FeedbackListItemDto
            {
                FeedbackId = f.FeedbackId,
                UserId = f.UserId,
                UserName = f.User.FullName,
                AreaId = f.AreaId,
                AreaName = f.Area.AreaName,
                CategoryId = f.CategoryId,
                CategoryName = f.Category.CategoryName,
                Title = f.Title,
                LocationText = f.LocationText,
                Priority = f.Priority,
                Status = f.Status,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt,
                AttachmentCount = f.FeedbackAttachments.Count,
                CommentCount = f.FeedbackComments.Count,
                SupportCount = f.FeedbackSupports.Count,
                DuplicateWarning = f.FeedbackDuplicateCandidates.Any(candidate => candidate.Status == "Pending"),
                ParentTicketId = f.ParentTicketId,
                IsMasterTicket = f.IsMasterTicket
            })
            .ToListAsync();

        return new PagedResultDto<FeedbackListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<PagedResultDto<FeedbackListItemDto>> GetAllFeedbacksAsync(FeedbackQueryParameters query)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var search = query.Search?.Trim().ToLower();
        var status = query.Status?.Trim().ToLower();

        var feedbacks = _uow.GetRepository<Feedback>().Entities
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            feedbacks = feedbacks.Where(f => f.Status.ToLower() == status);
        }

        if (query.CategoryId.HasValue)
        {
            feedbacks = feedbacks.Where(f => f.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            feedbacks = feedbacks.Where(f =>
                f.Title.ToLower().Contains(search) ||
                f.Description.ToLower().Contains(search) ||
                f.LocationText.ToLower().Contains(search));
        }

        var totalItems = await feedbacks.CountAsync();
        var items = await feedbacks
            .OrderByDescending(f => f.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FeedbackListItemDto
            {
                FeedbackId = f.FeedbackId,
                UserId = f.UserId,
                UserName = f.User.FullName,
                AreaId = f.AreaId,
                AreaName = f.Area.AreaName,
                CategoryId = f.CategoryId,
                CategoryName = f.Category.CategoryName,
                Title = f.Title,
                LocationText = f.LocationText,
                Priority = f.Priority,
                Status = f.Status,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt,
                AttachmentCount = f.FeedbackAttachments.Count,
                CommentCount = f.FeedbackComments.Count,
                SupportCount = f.FeedbackSupports.Count,
                DuplicateWarning = f.FeedbackDuplicateCandidates.Any(candidate => candidate.Status == "Pending"),
                ParentTicketId = f.ParentTicketId,
                IsMasterTicket = f.IsMasterTicket
            })
            .ToListAsync();

        return new PagedResultDto<FeedbackListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<PagedResultDto<FeedbackWithAnalysisResultDto>> GetAiReviewedFeedbacksAsync(FeedbackQueryParameters query)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var search = query.Search?.Trim().ToLower();

        var feedbacks = _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Where(f => f.Status.ToLower() == FeedbackStatus.AiReviewed.ToLower());

        if (query.CategoryId.HasValue)
        {
            feedbacks = feedbacks.Where(f => f.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            feedbacks = feedbacks.Where(f =>
                f.Title.ToLower().Contains(search) ||
                f.Description.ToLower().Contains(search) ||
                f.LocationText.ToLower().Contains(search));
        }

        var totalItems = await feedbacks.CountAsync();
        var rows = await feedbacks
            .OrderByDescending(f => f.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new
            {
                Feedback = new FeedbackListItemDto
                {
                    FeedbackId = f.FeedbackId,
                    UserId = f.UserId,
                    UserName = f.User.FullName,
                    AreaId = f.AreaId,
                    AreaName = f.Area.AreaName,
                    CategoryId = f.CategoryId,
                    CategoryName = f.Category.CategoryName,
                    Title = f.Title,
                    LocationText = f.LocationText,
                    Priority = f.Priority,
                    Status = f.Status,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt,
                    AttachmentCount = f.FeedbackAttachments.Count,
                    CommentCount = f.FeedbackComments.Count,
                    SupportCount = f.FeedbackSupports.Count,
                    DuplicateWarning = f.FeedbackDuplicateCandidates.Any(candidate => candidate.Status == "Pending"),
                    ParentTicketId = f.ParentTicketId,
                    IsMasterTicket = f.IsMasterTicket
                },
                AnalysisResult = f.AnalysisResults
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new
                    {
                        a.AnalysisResultId,
                        a.FeedbackId,
                        a.ModelName,
                        a.DetectedCategoryId,
                        DetectedCategoryName = a.DetectedCategory == null
                            ? null
                            : a.DetectedCategory.CategoryName,
                        a.Sentiment,
                        a.UrgencyLevel,
                        a.Summary,
                        a.Keywords,
                        a.ConfidenceScore,
                        a.RawResponse,
                        a.CreatedAt
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var items = rows
            .Select(row => new FeedbackWithAnalysisResultDto
            {
                Feedback = row.Feedback,
                AnalysisResult = row.AnalysisResult == null
                    ? null
                    : new AiAnalysisResponseDto
                    {
                        AnalysisResultId = row.AnalysisResult.AnalysisResultId,
                        FeedbackId = row.AnalysisResult.FeedbackId,
                        ModelName = row.AnalysisResult.ModelName,
                        DetectedCategoryId = row.AnalysisResult.DetectedCategoryId,
                        DetectedCategoryName = row.AnalysisResult.DetectedCategoryName,
                        Sentiment = row.AnalysisResult.Sentiment,
                        UrgencyLevel = row.AnalysisResult.UrgencyLevel,
                        Summary = row.AnalysisResult.Summary,
                        Keywords = ParseAnalysisKeywords(row.AnalysisResult.Keywords),
                        ConfidenceScore = row.AnalysisResult.ConfidenceScore,
                        RawResponse = row.AnalysisResult.RawResponse,
                        CreatedAt = row.AnalysisResult.CreatedAt
                    }
            })
            .ToList();

        return new PagedResultDto<FeedbackWithAnalysisResultDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<FeedbackDetailDto> GetFeedbackDetailAsync(Guid currentUserId, Guid feedbackId)
    {
        var feedback = await GetFeedbackWithDetailsAsync(feedbackId, asNoTracking: true);
        var detail = MapDetail(feedback, currentUserId);
        await PopulateDuplicateInfoAsync(detail);
        return detail;
    }

    public async Task<FeedbackDetailDto> UpdateAsync(Guid userId, Guid feedbackId, FeedbackUpdateRequest request)
    {
        var feedback = await GetOwnedFeedbackWithDetailsAsync(userId, feedbackId, asNoTracking: false);

        var updatedAreaId = request.AreaId ?? feedback.AreaId;
        var updatedLatitude = request.Latitude ?? feedback.Latitude;
        var updatedLongitude = request.Longitude ?? feedback.Longitude;
        await EnsureAreaMatchesLocationAsync(updatedAreaId, updatedLatitude, updatedLongitude);

        if (request.AreaId.HasValue && request.AreaId.Value != feedback.AreaId)
        {
            feedback.AreaId = request.AreaId.Value;
        }

        if (request.CategoryId.HasValue && request.CategoryId.Value != feedback.CategoryId)
        {
            await EnsureCategoryExistsAsync(request.CategoryId.Value);
            feedback.CategoryId = request.CategoryId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            feedback.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            feedback.Description = request.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.LocationText))
        {
            feedback.LocationText = request.LocationText.Trim();
        }

        feedback.Latitude = request.Latitude ?? feedback.Latitude;
        feedback.Longitude = request.Longitude ?? feedback.Longitude;
        feedback.LocationAccuracyMeters = request.LocationAccuracyMeters ?? feedback.LocationAccuracyMeters;
        feedback.GeoSource = request.GeoSource != null ? NormalizeOptional(request.GeoSource) : feedback.GeoSource;
        feedback.Priority = string.IsNullOrWhiteSpace(request.Priority) ? feedback.Priority : request.Priority.Trim();
        feedback.DueDate = request.DueDate ?? feedback.DueDate;
        feedback.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveAsync();
        await SendFeedbackNotificationAsync(
            feedback,
            "Phản ánh đã được cập nhật",
            $"Phản ánh \"{feedback.Title}\" của bạn đã được cập nhật thành công.");

        return await GetMyFeedbackDetailAsync(userId, feedbackId);
    }

    public async Task<FeedbackDetailDto> UpdateByStaffAsync(
    Guid currentUserId,
    Guid feedbackId,
    StaffFeedbackUpdateRequest request)
    {
        var feedback = await GetFeedbackWithDetailsAsync(
            feedbackId,
            asNoTracking: false);


        var oldCategoryId = feedback.CategoryId;

        var oldPriority = feedback.Priority;



        var hasContentChanges =
            request.AreaId.HasValue ||
            request.CategoryId.HasValue ||
            !string.IsNullOrWhiteSpace(request.Title) ||
            !string.IsNullOrWhiteSpace(request.Description) ||
            !string.IsNullOrWhiteSpace(request.LocationText) ||
            request.Latitude.HasValue ||
            request.Longitude.HasValue ||
            request.LocationAccuracyMeters.HasValue ||
            request.GeoSource != null ||
            !string.IsNullOrWhiteSpace(request.Priority) ||
            request.DueDate.HasValue;



        var updatedAreaId =
            request.AreaId ?? feedback.AreaId;


        var updatedLatitude =
            request.Latitude ?? feedback.Latitude;


        var updatedLongitude =
            request.Longitude ?? feedback.Longitude;



        await EnsureAreaMatchesLocationAsync(
            updatedAreaId,
            updatedLatitude,
            updatedLongitude);



        if (request.AreaId.HasValue &&
            request.AreaId.Value != feedback.AreaId)
        {
            feedback.AreaId =
                request.AreaId.Value;
        }



        if (request.CategoryId.HasValue &&
            request.CategoryId.Value != feedback.CategoryId)
        {
            await EnsureCategoryExistsAsync(
                request.CategoryId.Value);


            feedback.CategoryId =
                request.CategoryId.Value;
        }



        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            feedback.Title =
                request.Title.Trim();
        }



        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            feedback.Description =
                request.Description.Trim();
        }



        if (!string.IsNullOrWhiteSpace(request.LocationText))
        {
            feedback.LocationText =
                request.LocationText.Trim();
        }



        feedback.Latitude =
            request.Latitude ??
            feedback.Latitude;


        feedback.Longitude =
            request.Longitude ??
            feedback.Longitude;



        feedback.LocationAccuracyMeters =
            request.LocationAccuracyMeters ??
            feedback.LocationAccuracyMeters;



        feedback.GeoSource =
            request.GeoSource != null
            ? NormalizeOptional(request.GeoSource)
            : feedback.GeoSource;



        feedback.IsLocationVerified = true;



        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            feedback.Priority =
                request.Priority.Trim();
        }



        feedback.DueDate =
            request.DueDate ??
            feedback.DueDate;



        feedback.UpdatedAt =
            DateTime.UtcNow;



        /*
         * Kiểm tra thay đổi ảnh hưởng SLA
         */
        var categoryChanged =
            oldCategoryId != feedback.CategoryId;



        var priorityChanged =
            !string.Equals(
                oldPriority,
                feedback.Priority,
                StringComparison.OrdinalIgnoreCase);



        if(categoryChanged || priorityChanged)
{
            await _slaService.RecalculateAsync(
                feedback.FeedbackId,
                currentUserId,
                new RecalculateSlaRequest
                {
                    CategoryId =
                        feedback.CategoryId,

                    Priority =
                        feedback.Priority,

                    Note =
                        $"Staff cập nhật SLA. " +
                        $"Category: {oldCategoryId} -> {feedback.CategoryId}. " +
                        $"Priority: {oldPriority} -> {feedback.Priority}."
                });
        }



        FeedbackStatusHistory? statusHistory = null;

        string? oldStatus = null;



        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !string.Equals(
                feedback.Status,
                request.Status.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            var newStatus =
                FeedbackStatus.Normalize(
                    request.Status);



            await EnsureDuplicateMasterStatusInvariantAsync(
                feedback,
                newStatus);



            await EnsureDuplicateReviewCompletedBeforeWorkflowAsync(
                feedback,
                newStatus);



            oldStatus =
                feedback.Status;



            statusHistory =
                new FeedbackStatusHistory
                {
                    FeedbackId =
                        feedbackId,

                    ChangedByUserId =
                        currentUserId,

                    OldStatus =
                        oldStatus,

                    NewStatus =
                        newStatus,

                    Note =
                        request.StatusNote?.Trim(),

                    ChangedAt =
                        DateTime.UtcNow
                };



            feedback.Status =
                newStatus;



            feedback.FeedbackStatusHistories.Add(
                statusHistory);
        }



        await _uow.SaveAsync();



        if (statusHistory != null &&
            oldStatus != null)
        {
            await SynchronizeSlaByStatusAsync(
                feedback.FeedbackId,
                oldStatus,
                feedback.Status,
                currentUserId,
                statusHistory.Note);



            await SendStatusUpdatedNotificationAsync(
                feedback,
                statusHistory);
        }



        if (hasContentChanges)
        {
            await SendFeedbackNotificationAsync(
                feedback,
                "Phản ánh đã được nhân viên cập nhật",
                $"Thông tin phản ánh \"{feedback.Title}\" đã được nhân viên cập nhật.");
        }



        return await GetFeedbackDetailAsync(
            currentUserId,
            feedbackId);
    }

    public async Task DeleteAsync(Guid userId, Guid feedbackId)
    {
        var feedback = await GetOwnedFeedbackWithDetailsAsync(userId, feedbackId, asNoTracking: false);
        _uow.GetRepository<Feedback>().Delete(feedback);
        await _uow.SaveAsync();
    }

    public async Task<FeedbackDetailDto> AddAttachmentsAsync(
        Guid userId,
        Guid feedbackId,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments)
    {
        if (attachments.Count == 0)
        {
            throw new Exception("Vui lòng chọn ít nhất một file.");
        }

        var feedback = await GetOwnedFeedbackWithDetailsAsync(userId, feedbackId, asNoTracking: false);
        var now = DateTime.UtcNow;

        foreach (var attachment in attachments)
        {
            feedback.FeedbackAttachments.Add(new FeedbackAttachment
            {
                FeedbackId = feedbackId,
                FileUrl = attachment.FileUrl,
                FileType = attachment.FileType,
                UploadedAt = now
            });
        }

        feedback.UpdatedAt = now;
        await _uow.SaveAsync();

        return await GetMyFeedbackDetailAsync(userId, feedbackId);
    }

    public async Task DeleteAttachmentAsync(Guid userId, Guid feedbackId, int attachmentId)
    {
        var feedback = await GetOwnedFeedbackWithDetailsAsync(userId, feedbackId, asNoTracking: false);
        var attachment = feedback.FeedbackAttachments.FirstOrDefault(a => a.AttachmentId == attachmentId);

        if (attachment == null)
        {
            throw new Exception("Không tìm thấy attachment.");
        }

        _uow.GetRepository<FeedbackAttachment>().Delete(attachment);
        feedback.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
    }

    public async Task<FeedbackStatusHistoryDto>
    UpdateStatusByStaffOrAdminAsync(
        Guid currentUserId,
        Guid feedbackId,
        UpdateFeedbackStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new Exception(
                "Status là bắt buộc.");
        }

        var feedback = await GetFeedbackWithDetailsAsync(
            feedbackId,
            asNoTracking: false);

        var newStatus = FeedbackStatus.Normalize(
            request.Status);

        if (string.Equals(
                feedback.Status,
                newStatus,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                $"Feedback đã ở trạng thái {newStatus}.");
        }

        await EnsureDuplicateMasterStatusInvariantAsync(feedback, newStatus);
        await EnsureDuplicateReviewCompletedBeforeWorkflowAsync(feedback, newStatus);

        var now = DateTime.UtcNow;
        var oldStatus = feedback.Status;

        var history = new FeedbackStatusHistory
        {
            FeedbackId = feedbackId,
            ChangedByUserId = currentUserId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = request.Note?.Trim(),
            ChangedAt = now
        };

        feedback.Status = newStatus;
        feedback.UpdatedAt = now;
        feedback.FeedbackStatusHistories.Add(history);

        await _uow.SaveAsync();

        await SynchronizeSlaByStatusAsync(
            feedback.FeedbackId,
            oldStatus,
            newStatus,
            currentUserId,
            history.Note);

        await SendStatusUpdatedNotificationAsync(
            feedback,
            history);

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


    public async Task<FeedbackCommentDto> AddCommentAsync(Guid userId, Guid feedbackId, FeedbackCommentCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new Exception("Nội dung comment là bắt buộc.");
        }

        await EnsureFeedbackExistsAsync(feedbackId);

        var comment = new FeedbackComment
        {
            FeedbackId = feedbackId,
            UserId = userId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<FeedbackComment>().AddAsync(comment);
        await _uow.SaveAsync();

        var saved = await _uow.GetRepository<FeedbackComment>().FindAsync(
            c => c.CommentId == comment.CommentId,
            q => q.Include(c => c.User));

        return MapComment(saved!);
    }

    public async Task SupportAsync(Guid userId, Guid feedbackId)
    {
        await EnsureFeedbackExistsAsync(feedbackId);

        var supportRepo = _uow.GetRepository<FeedbackSupport>();
        var existingSupport = await supportRepo.FindAsync(
            s => s.FeedbackId == feedbackId && s.UserId == userId,
            include: null);

        if (existingSupport != null)
        {
            return;
        }

        await supportRepo.AddAsync(new FeedbackSupport
        {
            FeedbackId = feedbackId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _uow.SaveAsync();
    }

    public async Task UnsupportAsync(Guid userId, Guid feedbackId)
    {
        var support = await _uow.GetRepository<FeedbackSupport>().FindAsync(
            s => s.FeedbackId == feedbackId && s.UserId == userId,
            include: null);

        if (support == null)
        {
            return;
        }

        _uow.GetRepository<FeedbackSupport>().Delete(support);
        await _uow.SaveAsync();
    }

    private static readonly string[] InternalFeedbackStatuses =
    [
        FeedbackStatus.Submitted,
        FeedbackStatus.AiReviewed
    ];

    private static bool IsInternalFeedbackStatus(string status)
    {
        return InternalFeedbackStatuses.Any(internalStatus =>
            string.Equals(internalStatus, status, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureDuplicateMasterStatusInvariantAsync(
        Feedback feedback,
        string newStatus)
    {
        if (FeedbackStatus.IsEligibleDuplicateMasterStatus(newStatus))
        {
            return;
        }

        var hasLinkedDuplicates = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .AnyAsync(child => child.ParentTicketId == feedback.FeedbackId);

        if (!hasLinkedDuplicates)
        {
            hasLinkedDuplicates = await _uow
                .GetRepository<FeedbackDuplicateCandidate>()
                .Entities
                .AsNoTracking()
                .AnyAsync(candidate =>
                    candidate.PotentialParentFeedbackId == feedback.FeedbackId &&
                    candidate.Status == "Confirmed");
        }

        if (hasLinkedDuplicates)
        {
            throw new Exception(
                "Phản ánh đang là phản ánh chính của các phản ánh trùng nên phải giữ trạng thái công khai, hợp lệ.");
        }
    }

    private async Task EnsureDuplicateReviewCompletedBeforeWorkflowAsync(
        Feedback feedback,
        string newStatus)
    {
        if (feedback.ParentTicketId.HasValue)
        {
            throw new Exception(
                "Phản ánh đã được đánh dấu trùng và được xử lý theo phản ánh chính; không thể cập nhật quy trình riêng.");
        }

        if (string.Equals(newStatus, FeedbackStatus.Submitted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(newStatus, FeedbackStatus.AiReviewed, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hasPendingDuplicateReview = await _uow
            .GetRepository<FeedbackDuplicateCandidate>()
            .Entities
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.FeedbackId == feedback.FeedbackId &&
                candidate.Status == "Pending");

        if (hasPendingDuplicateReview)
        {
            throw new Exception(
                "Phản ánh đang chờ xác nhận trùng; cần xác nhận hoặc từ chối đề xuất trước khi tiếp tục quy trình xử lý.");
        }
    }

    private async Task<Feedback> GetFeedbackWithDetailsAsync(Guid feedbackId, bool asNoTracking)
    {
        IQueryable<Feedback> query = _uow.GetRepository<Feedback>().Entities;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        var feedback = await query
            .Include(f => f.User)
            .Include(f => f.Area)
            .Include(f => f.Category)
            .Include(f => f.FeedbackAttachments)
            .Include(f => f.FeedbackComments)
                .ThenInclude(c => c.User)
            .Include(f => f.FeedbackStatusHistories)
                .ThenInclude(h => h.ChangedByUser)
            .Include(f => f.FeedbackSupports)
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId);

        return feedback ?? throw new Exception("Không tìm thấy feedback.");
    }

    private async Task SendStatusUpdatedNotificationAsync(Feedback feedback, FeedbackStatusHistory history)
    {
        var message = (history.OldStatus, history.NewStatus) switch
        {
            (FeedbackStatus.Submitted, FeedbackStatus.AiReviewed) =>
                "Phản ánh đã được AI phân tích",

            (FeedbackStatus.Submitted, FeedbackStatus.Verified) or
            (FeedbackStatus.AiReviewed, FeedbackStatus.Verified) =>
                "Phản ánh của bạn đã được xác thực",

            (FeedbackStatus.Verified, FeedbackStatus.Assigned) =>
                "Phản ánh của bạn đã được phân công cho đơn vị xử lý",

            (FeedbackStatus.Assigned, FeedbackStatus.InProgress) =>
                "Đơn vị xử lý đang xử lý phản ánh của bạn.",

            (FeedbackStatus.InProgress, FeedbackStatus.SubmittedForApproval) =>
                "Đơn vị xử lý đã gửi minh chứng hoàn thành",

            (FeedbackStatus.SubmittedForApproval, FeedbackStatus.Approved) =>
                "Hệ thống đã xác nhận đơn vị xử lý đã hoàn thành",

            (FeedbackStatus.Approved, FeedbackStatus.Closed) =>
                "Phản ánh của bạn đã được hoàn thành",

            _ =>
                $"Phản ánh \"{feedback.Title}\" đã chuyển trạng thái từ \"{history.OldStatus}\" sang \"{history.NewStatus}\"."
        };

        await _notificationService.SendAsync(
            feedback.UserId,
            "Trạng thái phản ánh đã được cập nhật",
            message,
            NotificationType.TicketUpdated,
            $"/feedbacks/{feedback.FeedbackId}");
    }

    private async Task SendFeedbackNotificationAsync(
        Feedback feedback,
        string title,
        string message,
        string? targetUrl = null)
    {
        await _notificationService.SendAsync(
            feedback.UserId,
            title,
            message,
            NotificationType.TicketUpdated,
            targetUrl ?? $"/feedbacks/{feedback.FeedbackId}");
    }

    private async Task<Feedback> GetOwnedFeedbackWithDetailsAsync(Guid userId, Guid feedbackId, bool asNoTracking)
    {
        IQueryable<Feedback> query = _uow.GetRepository<Feedback>().Entities;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        var feedback = await query
            .Include(f => f.User)
            .Include(f => f.Area)
            .Include(f => f.Category)
            .Include(f => f.FeedbackAttachments)
            .Include(f => f.FeedbackComments)
                .ThenInclude(c => c.User)
            .Include(f => f.FeedbackStatusHistories)
                .ThenInclude(h => h.ChangedByUser)
            .Include(f => f.FeedbackSupports)
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId && f.UserId == userId);

        return feedback ?? throw new Exception("Không tìm thấy feedback.");
    }

    private async Task EnsureFeedbackExistsAsync(Guid feedbackId)
    {
        var exists = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .AnyAsync(f => f.FeedbackId == feedbackId);

        if (!exists)
        {
            throw new Exception("Không tìm thấy feedback.");
        }
    }

    private async Task EnsureCategoryExistsAsync(int categoryId)
    {
        var exists = await _uow.GetRepository<UrbanServiceCategory>().Entities
            .AsNoTracking()
            .AnyAsync(c => c.CategoryId == categoryId && c.IsActive);

        if (!exists)
        {
            throw new Exception("Category không tồn tại hoặc đã bị khóa.");
        }
    }

    private async Task EnsureAreaExistsAsync(int areaId)
    {
        var exists = await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .AnyAsync(a => a.AreaId == areaId && a.IsActive);

        if (!exists)
        {
            throw new Exception("Area khong ton tai hoac da bi khoa.");
        }
    }

    private async Task EnsureAreaMatchesLocationAsync(int areaId, decimal? latitude, decimal? longitude)
    {
        var area = await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .Where(a => a.AreaId == areaId && a.IsActive)
            .Select(a => new
            {
                a.AreaId,
                a.AreaName,
                a.BoundaryGeoJson
            })
            .FirstOrDefaultAsync();

        if (area == null)
        {
            throw new Exception("Area khong ton tai hoac da bi khoa.");
        }

        if (!latitude.HasValue || !longitude.HasValue)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(area.BoundaryGeoJson))
        {
            throw new Exception($"Khu vực \"{area.AreaName}\" chưa được cấu hình ranh giới bản đồ.");
        }

        if (!IsPointInsideGeoJsonBoundary(latitude.Value, longitude.Value, area.BoundaryGeoJson))
        {
            throw new Exception($"Vị trí đã chọn không nằm trong khu vực \"{area.AreaName}\".");
        }
    }

    private static bool IsPointInsideGeoJsonBoundary(decimal latitude, decimal longitude, string boundaryGeoJson)
    {
        try
        {
            using var document = JsonDocument.Parse(boundaryGeoJson);
            return IsPointInsideGeoJsonElement(document.RootElement, (double)latitude, (double)longitude);
        }
        catch (JsonException)
        {
            throw new Exception("BoundaryGeoJson của khu vực không hợp lệ.");
        }
    }

    private static bool IsPointInsideGeoJsonElement(JsonElement element, double latitude, double longitude)
    {
        if (!element.TryGetProperty("type", out var typeElement))
        {
            return false;
        }

        var type = typeElement.GetString();

        if (string.Equals(type, "FeatureCollection", StringComparison.OrdinalIgnoreCase))
        {
            if (!element.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var feature in features.EnumerateArray())
            {
                if (IsPointInsideGeoJsonElement(feature, latitude, longitude))
                {
                    return true;
                }
            }

            return false;
        }

        if (string.Equals(type, "Feature", StringComparison.OrdinalIgnoreCase))
        {
            return element.TryGetProperty("geometry", out var geometry) &&
                IsPointInsideGeoJsonElement(geometry, latitude, longitude);
        }

        if (!element.TryGetProperty("coordinates", out var coordinates))
        {
            return false;
        }

        if (string.Equals(type, "Polygon", StringComparison.OrdinalIgnoreCase))
        {
            return IsPointInsidePolygonCoordinates(coordinates, latitude, longitude);
        }

        if (string.Equals(type, "MultiPolygon", StringComparison.OrdinalIgnoreCase))
        {
            if (coordinates.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var polygon in coordinates.EnumerateArray())
            {
                if (IsPointInsidePolygonCoordinates(polygon, latitude, longitude))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPointInsidePolygonCoordinates(JsonElement polygonCoordinates, double latitude, double longitude)
    {
        if (polygonCoordinates.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var isInsideOuterRing = false;
        var isInsideHole = false;
        var ringIndex = 0;

        foreach (var ring in polygonCoordinates.EnumerateArray())
        {
            var isInsideRing = IsPointInsideLinearRing(ring, latitude, longitude);

            if (ringIndex == 0)
            {
                isInsideOuterRing = isInsideRing;
            }
            else if (isInsideRing)
            {
                isInsideHole = true;
                break;
            }

            ringIndex++;
        }

        return isInsideOuterRing && !isInsideHole;
    }

    private static bool IsPointInsideLinearRing(JsonElement ring, double latitude, double longitude)
    {
        if (ring.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var points = ring.EnumerateArray()
            .Where(point => point.ValueKind == JsonValueKind.Array && point.GetArrayLength() >= 2)
            .Select(point => new
            {
                Longitude = point[0].GetDouble(),
                Latitude = point[1].GetDouble()
            })
            .ToList();

        if (points.Count < 3)
        {
            return false;
        }

        var inside = false;
        var previousIndex = points.Count - 1;

        for (var currentIndex = 0; currentIndex < points.Count; currentIndex++)
        {
            var current = points[currentIndex];
            var previous = points[previousIndex];

            if (IsPointOnSegment(
                longitude,
                latitude,
                previous.Longitude,
                previous.Latitude,
                current.Longitude,
                current.Latitude))
            {
                return true;
            }

            var intersects = current.Latitude > latitude != previous.Latitude > latitude &&
                longitude < (previous.Longitude - current.Longitude) *
                (latitude - current.Latitude) /
                (previous.Latitude - current.Latitude) +
                current.Longitude;

            if (intersects)
            {
                inside = !inside;
            }

            previousIndex = currentIndex;
        }

        return inside;
    }

    private static bool IsPointOnSegment(
        double pointLongitude,
        double pointLatitude,
        double startLongitude,
        double startLatitude,
        double endLongitude,
        double endLatitude)
    {
        const double epsilon = 0.0000001;

        var crossProduct = (pointLatitude - startLatitude) * (endLongitude - startLongitude) -
            (pointLongitude - startLongitude) * (endLatitude - startLatitude);

        if (Math.Abs(crossProduct) > epsilon)
        {
            return false;
        }

        var dotProduct = (pointLongitude - startLongitude) * (endLongitude - startLongitude) +
            (pointLatitude - startLatitude) * (endLatitude - startLatitude);

        if (dotProduct < -epsilon)
        {
            return false;
        }

        var squaredLength = Math.Pow(endLongitude - startLongitude, 2) +
            Math.Pow(endLatitude - startLatitude, 2);

        return dotProduct <= squaredLength + epsilon;
    }

    private static FeedbackDetailDto MapDetail(Feedback feedback, Guid userId)
    {
        return new FeedbackDetailDto
        {
            FeedbackId = feedback.FeedbackId,
            UserId = feedback.UserId,
            UserName = feedback.User?.FullName,
            AreaId = feedback.AreaId,
            AreaName = feedback.Area?.AreaName,
            CategoryId = feedback.CategoryId,
            CategoryName = feedback.Category?.CategoryName,
            Title = feedback.Title,
            Description = feedback.Description,
            LocationText = feedback.LocationText,
            Latitude = feedback.Latitude,
            Longitude = feedback.Longitude,
            LocationAccuracyMeters = feedback.LocationAccuracyMeters,
            GeoSource = feedback.GeoSource,
            IsLocationVerified = feedback.IsLocationVerified,
            Priority = feedback.Priority,
            Status = feedback.Status,
            DueDate = feedback.DueDate,
            CreatedAt = feedback.CreatedAt,
            UpdatedAt = feedback.UpdatedAt,
            AttachmentCount = feedback.FeedbackAttachments.Count,
            CommentCount = feedback.FeedbackComments.Count,
            SupportCount = feedback.FeedbackSupports.Count,
            DuplicateWarning = false,
            PotentialDuplicate = null,
            ParentTicketId = feedback.ParentTicketId,
            IsMasterTicket = feedback.IsMasterTicket,
            IsSupportedByCurrentUser = feedback.FeedbackSupports.Any(s => s.UserId == userId),
            Attachments = feedback.FeedbackAttachments
                .OrderBy(a => a.UploadedAt)
                .Select(a => new FeedbackAttachmentDto
                {
                    AttachmentId = a.AttachmentId,
                    FileUrl = a.FileUrl,
                    FileType = a.FileType,
                    UploadedAt = a.UploadedAt
                })
                .ToList(),
            Comments = feedback.FeedbackComments
                .OrderBy(c => c.CreatedAt)
                .Select(MapComment)
                .ToList(),
            StatusHistories = feedback.FeedbackStatusHistories
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new FeedbackStatusHistoryDto
                {
                    HistoryId = h.HistoryId,
                    FeedbackId = h.FeedbackId,
                    ChangedByUserId = h.ChangedByUserId,
                    ChangedByUserName = h.ChangedByUser?.FullName,
                    OldStatus = h.OldStatus,
                    NewStatus = h.NewStatus,
                    Note = h.Note,
                    ChangedAt = h.ChangedAt
                })
                .ToList()
        };
    }

    private async Task PopulateDuplicateInfoAsync(FeedbackListItemDto dto)
    {
        var duplicateState = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Where(feedback => feedback.FeedbackId == dto.FeedbackId)
            .Select(feedback => new
            {
                feedback.ParentTicketId,
                feedback.IsMasterTicket
            })
            .FirstOrDefaultAsync();

        if (duplicateState is not null)
        {
            dto.ParentTicketId = duplicateState.ParentTicketId;
            dto.IsMasterTicket = duplicateState.IsMasterTicket;
        }

        var pendingCandidate = await _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .AsNoTracking()
            .Where(candidate =>
                candidate.FeedbackId == dto.FeedbackId &&
                candidate.Status == "Pending")
            .OrderByDescending(candidate => candidate.ConfidenceScore ?? 0m)
            .ThenByDescending(candidate => candidate.CreatedAt)
            .Select(candidate => new FeedbackPotentialDuplicateDto
            {
                DuplicateCandidateId = candidate.DuplicateCandidateId,
                FeedbackId = candidate.FeedbackId,
                PotentialParentFeedbackId = candidate.PotentialParentFeedbackId,
                PotentialParentTitle = candidate.PotentialParentFeedback.Title,
                PotentialParentLocationText = candidate.PotentialParentFeedback.LocationText,
                Status = candidate.Status,
                ConfidenceScore = candidate.ConfidenceScore,
                Reason = candidate.Reason,
                CreatedAt = candidate.CreatedAt
            })
            .FirstOrDefaultAsync();

        dto.PotentialDuplicate = pendingCandidate;
        dto.DuplicateWarning = pendingCandidate is not null;
    }

    private static FeedbackCommentDto MapComment(FeedbackComment comment)
    {
        return new FeedbackCommentDto
        {
            CommentId = comment.CommentId,
            FeedbackId = comment.FeedbackId,
            UserId = comment.UserId,
            UserName = comment.User?.FullName,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt
        };
    }

    private async Task<FeedbackProviderReportDto> GetProviderReportDtoAsync(int providerReportId)
    {
        var report = await _uow.GetRepository<FeedbackProviderReport>().Entities
            .AsNoTracking()
            .Include(r => r.Coordinator)
            .Include(r => r.ReportedByUser)
            .Include(r => r.ProviderContactLogs)
            .Include(r => r.CompletionDocuments)
            .FirstOrDefaultAsync(r => r.ProviderReportId == providerReportId)
            ?? throw new Exception("Provider report khong ton tai.");

        return MapProviderReport(report);
    }

    private async Task EnsureProviderReportExistsAsync(int providerReportId)
    {
        var exists = await _uow.GetRepository<FeedbackProviderReport>().Entities
            .AsNoTracking()
            .AnyAsync(r => r.ProviderReportId == providerReportId);

        if (!exists)
        {
            throw new Exception("Provider report khong ton tai.");
        }
    }

    private static FeedbackProviderReportDto MapProviderReport(FeedbackProviderReport report)
    {
        return new FeedbackProviderReportDto
        {
            ProviderReportId = report.ProviderReportId,
            FeedbackId = report.FeedbackId,
            CoordinatorId = report.CoordinatorId,
            ProviderName = report.Coordinator?.ProviderName,
            CoordinatorName = report.Coordinator?.CoordinatorName,
            PhoneNumber = report.Coordinator?.PhoneNumber,
            Email = report.Coordinator?.Email,
            Address = report.Coordinator?.Address,
            Note = report.Coordinator?.Note,
            ReportedByUserId = report.ReportedByUserId,
            ReportedByUserName = report.ReportedByUser?.FullName,
            ReportStatus = report.ReportStatus,
            DueDate = report.DueDate,
            ReportNote = report.ReportNote,
            ReportedAt = report.ReportedAt,
            UpdatedAt = report.UpdatedAt,
            ContactLogCount = report.ProviderContactLogs.Count,
            CompletionDocumentCount = report.CompletionDocuments.Count
        };
    }

    private static ProviderContactLogDto MapContactLog(ProviderContactLog log)
    {
        return new ProviderContactLogDto
        {
            ContactLogId = log.ContactLogId,
            ProviderReportId = log.ProviderReportId,
            CoordinatorId = log.CoordinatorId,
            ProviderName = log.Coordinator?.ProviderName,
            CoordinatorName = log.Coordinator?.CoordinatorName,
            PhoneNumber = log.Coordinator?.PhoneNumber,
            Email = log.Coordinator?.Email,
            Address = log.Coordinator?.Address,
            Note = log.Coordinator?.Note,
            ContactedByUserId = log.ContactedByUserId,
            ContactedByUserName = log.ContactedByUser?.FullName,
            ContactMethod = log.ContactMethod,
            ContactResult = log.ContactResult,
            ContactNote = log.ContactNote,
            ContactedAt = log.ContactedAt
        };
    }

    private static CompletionDocumentDto MapCompletionDocument(CompletionDocument document)
    {
        return new CompletionDocumentDto
        {
            CompletionDocumentId = document.CompletionDocumentId,
            ProviderReportId = document.ProviderReportId,
            FeedbackId = document.FeedbackId,
            CoordinatorId = document.CoordinatorId,
            ProviderName = document.Coordinator?.ProviderName,
            UploadedByUserId = document.UploadedByUserId,
            UploadedByUserName = document.UploadedByUser?.FullName,
            FileUrl = document.FileUrl,
            FileType = document.FileType,
            Description = document.Description,
            ReceivedAt = document.ReceivedAt
        };
    }

    private static FeedbackResolutionDto MapResolution(FeedbackResolution resolution)
    {
        return new FeedbackResolutionDto
        {
            ResolutionId = resolution.ResolutionId,
            FeedbackId = resolution.FeedbackId,
            ProviderReportId = resolution.ProviderReportId,
            CreatedByStaffUserId = resolution.CreatedByStaffUserId,
            CreatedByStaffUserName = resolution.CreatedByStaffUser?.FullName,
            ResolutionSummary = resolution.ResolutionSummary,
            ActionTaken = resolution.ActionTaken,
            ResultNote = resolution.ResultNote,
            ResolvedAt = resolution.ResolvedAt,
            Status = resolution.Status
        };
    }

    private static FeedbackResolutionReviewDto MapResolutionReview(FeedbackResolutionReview review)
    {
        return new FeedbackResolutionReviewDto
        {
            ReviewId = review.ReviewId,
            FeedbackId = review.FeedbackId,
            UserId = review.UserId,
            UserName = review.User?.FullName,
            Rating = review.Rating ?? 0,
            IsSatisfied = review.IsSatisfied ?? false,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        };
    }

    private static IReadOnlyCollection<string> ParseAnalysisKeywords(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(keywords) ?? [];
        }
        catch (JsonException)
        {
            return keywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    private static void ValidateCreate(FeedbackCreateRequest request)
    {
        if (request.AreaId <= 0)
        {
            throw new Exception("AreaId la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new Exception("Title là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new Exception("Description là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(request.LocationText))
        {
            throw new Exception("LocationText là bắt buộc.");
        }
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeProviderReportStatus(string status)
    {
        var normalized = AllowedProviderReportStatuses.FirstOrDefault(
            allowed => string.Equals(allowed, status.Trim(), StringComparison.OrdinalIgnoreCase));

        return normalized ?? throw new Exception(
            $"Provider report status khong hop le. Cac gia tri duoc phep: {string.Join(", ", AllowedProviderReportStatuses)}.");
    }

    private async Task<FeedbackStatusHistory> ChangeStatusAsync(
    Feedback feedback,
    string newStatus,
    Guid userId,
    string? note = null)
    {
        var oldStatus = feedback.Status;
        var normalizedStatus = FeedbackStatus.Normalize(newStatus);
        await EnsureDuplicateReviewCompletedBeforeWorkflowAsync(feedback, normalizedStatus);

        feedback.Status = normalizedStatus;
        feedback.UpdatedAt = DateTime.UtcNow;

        var history = new FeedbackStatusHistory
        {
            FeedbackId = feedback.FeedbackId,
            ChangedByUserId = userId,
            OldStatus = oldStatus,
            NewStatus = feedback.Status,
            Note = note,
            ChangedAt = DateTime.UtcNow
        };

        await _uow
            .GetRepository<FeedbackStatusHistory>()
            .AddAsync(history);

        return history;
    }

    public async Task VerifyFeedbackAsync(
    Guid feedbackId,
    Guid staffUserId)
    {
        var feedback = await GetFeedbackWithDetailsAsync(
            feedbackId,
            false);

        if (feedback.Status != FeedbackStatus.Submitted &&
            feedback.Status != FeedbackStatus.AiReviewed)
        {
            throw new Exception(
                "Feedback must be Submitted or AiReviewed.");
        }

        var oldStatus = feedback.Status;

        var history = await ChangeStatusAsync(
            feedback,
            FeedbackStatus.Verified,
            staffUserId,
            "Verified by staff");

        await _uow.SaveAsync();

        // SLA bắt đầu ngay sau khi feedback được staff xác minh.
        await SynchronizeSlaByStatusAsync(
            feedback.FeedbackId,
            oldStatus,
            feedback.Status,
            staffUserId,
            history.Note);

        await SendStatusUpdatedNotificationAsync(
            feedback,
            history);
    }

    public async Task<FeedbackProviderReportDto> AssignFeedbackAsync(
    AssignFeedbackRequest request)
    {
        _uow.BeginTransaction();

        try
        {
            var feedback =
                await GetFeedbackWithDetailsAsync(
                    request.FeedbackId,
                    false);

            if (feedback.Status != FeedbackStatus.Verified)
                throw new Exception(
                    "Feedback must be Verified.");

            var coordinatorExists = await _uow
                .GetRepository<ServiceProviderCoordinator>()
                .Entities
                .AsNoTracking()
                .AnyAsync(c => c.CoordinatorId == request.CoordinatorId && c.IsActive);

            if (!coordinatorExists)
                throw new Exception("Coordinator khong ton tai hoac da bi khoa.");

            var report =
                new FeedbackProviderReport
                {
                    FeedbackId =
                        request.FeedbackId,

                    CoordinatorId =
                        request.CoordinatorId,

                    ReportedByUserId =
                        request.StaffUserId,

                    ReportStatus =
                        "Reported",

                    ReportNote =
                        request.Note,

                    ReportedAt =
                        DateTime.UtcNow
                };

            await _uow
                .GetRepository<FeedbackProviderReport>()
                .AddAsync(report);

            var history = await ChangeStatusAsync(
                feedback,
                FeedbackStatus.Assigned,
                request.StaffUserId);

            await _uow.SaveAsync();

            _uow.CommitTransaction();
            await SendStatusUpdatedNotificationAsync(feedback, history);

            return await GetProviderReportDtoAsync(report.ProviderReportId);
        }
        catch
        {
            _uow.RollBack();
            throw;
        }
    }

    public async Task<IReadOnlyCollection<ProviderCandidateDto>> GetProviderCandidatesAsync(Guid feedbackId)
    {
        var feedback = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId)
            ?? throw new Exception("Khong tim thay feedback.");

        var coverages = await _uow.GetRepository<CoordinatorCoverage>().Entities
            .AsNoTracking()
            .Include(c => c.Coordinator)
                .ThenInclude(c => c.ProviderContracts)
            .Where(c =>
                c.AreaId == feedback.AreaId &&
                c.CategoryId == feedback.CategoryId &&
                c.IsActive &&
                c.Coordinator.IsActive)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.PriorityOrder)
            .ThenBy(c => c.Coordinator.ProviderName)
            .ToListAsync();

        return coverages
            .Select(coverage =>
            {
                var contract = coverage.Coordinator.ProviderContracts
                    .Where(contract =>
                        (contract.AreaId == null || contract.AreaId == feedback.AreaId) &&
                        (contract.CategoryId == null || contract.CategoryId == feedback.CategoryId))
                    .OrderByDescending(contract =>
                        string.Equals(contract.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(contract => contract.AreaId == feedback.AreaId && contract.CategoryId == feedback.CategoryId)
                    .ThenByDescending(contract => contract.CreatedAt)
                    .FirstOrDefault();

                return new ProviderCandidateDto
                {
                    CoordinatorId = coverage.CoordinatorId,
                    ProviderName = coverage.Coordinator.ProviderName,
                    CoordinatorName = coverage.Coordinator.CoordinatorName,
                    PhoneNumber = coverage.Coordinator.PhoneNumber,
                    Email = coverage.Coordinator.Email,
                    Address = coverage.Coordinator.Address,
                    Note = coverage.Coordinator.Note,
                    IsPrimary = coverage.IsPrimary,
                    PriorityOrder = coverage.PriorityOrder,
                    ContractId = contract?.ContractId,
                    ContractCode = contract?.ContractCode,
                    ContractName = contract?.ContractName,
                    ContractStatus = contract?.Status
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<FeedbackProviderReportDto>> GetProviderReportsAsync(Guid feedbackId)
    {
        await EnsureFeedbackExistsAsync(feedbackId);

        var reports = await _uow.GetRepository<FeedbackProviderReport>().Entities
            .AsNoTracking()
            .Include(r => r.Coordinator)
            .Include(r => r.ReportedByUser)
            .Include(r => r.ProviderContactLogs)
            .Include(r => r.CompletionDocuments)
            .Where(r => r.FeedbackId == feedbackId)
            .OrderByDescending(r => r.ReportedAt)
            .ToListAsync();

        return reports
            .Select(MapProviderReport)
            .ToList();
    }

    public async Task<FeedbackProviderReportDto> UpdateProviderReportStatusAsync(
        int providerReportId,
        Guid currentUserId,
        UpdateProviderReportStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new Exception("Status la bat buoc.");
        }

        var report = await _uow.GetRepository<FeedbackProviderReport>().Entities
            .Include(r => r.Feedback)
            .FirstOrDefaultAsync(r => r.ProviderReportId == providerReportId)
            ?? throw new Exception("Provider report khong ton tai.");

        var newStatus = NormalizeProviderReportStatus(request.Status);
        report.ReportStatus = newStatus;
        report.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            report.ReportNote = request.Note.Trim();
        }

        FeedbackStatusHistory? statusHistory = null;
        if (newStatus == "InProgress" &&
            (report.Feedback.Status == FeedbackStatus.Assigned ||
             report.Feedback.Status == FeedbackStatus.NeedRework))
        {
            statusHistory = await ChangeStatusAsync(
                report.Feedback,
                FeedbackStatus.InProgress,
                currentUserId,
                request.Note);
        }

        await _uow.SaveAsync();

        if (statusHistory != null)
        {
            await SynchronizeSlaByStatusAsync(
                report.Feedback.FeedbackId,
                statusHistory.OldStatus!,
                statusHistory.NewStatus,
                currentUserId,
                statusHistory.Note);

            await SendStatusUpdatedNotificationAsync(
                report.Feedback,
                statusHistory);
        }

        await SendFeedbackNotificationAsync(
    report.Feedback,
    "Trạng thái nhà cung cấp đã được cập nhật",
    $"Phản ánh \"{report.Feedback.Title}\" có trạng thái nhà cung cấp mới: {newStatus}.");

        return await GetProviderReportDtoAsync(providerReportId);
    }

    public async Task<ProviderContactLogDto> AddProviderContactLogAsync(
        int providerReportId,
        Guid currentUserId,
        ProviderContactLogCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContactMethod))
        {
            throw new Exception("ContactMethod la bat buoc.");
        }

        var report = await _uow.GetRepository<FeedbackProviderReport>().Entities
            .Include(r => r.Feedback)
            .FirstOrDefaultAsync(r => r.ProviderReportId == providerReportId)
            ?? throw new Exception("Provider report khong ton tai.");

        var now = DateTime.UtcNow;
        var log = new ProviderContactLog
        {
            ProviderReportId = providerReportId,
            CoordinatorId = report.CoordinatorId,
            ContactedByUserId = currentUserId,
            ContactMethod = request.ContactMethod.Trim(),
            ContactResult = NormalizeOptional(request.ContactResult),
            ContactNote = NormalizeOptional(request.ContactNote),
            ContactedAt = request.ContactedAt ?? now
        };

        await _uow.GetRepository<ProviderContactLog>().AddAsync(log);

        FeedbackStatusHistory? statusHistory = null;
        var isSuccessfulContact = IsSuccessfulCoordinatorContact(log.ContactResult);

        if (string.Equals(report.ReportStatus, "Reported", StringComparison.OrdinalIgnoreCase) &&
            isSuccessfulContact)
        {
            report.ReportStatus = "InProgress";
            report.UpdatedAt = now;

            if (report.Feedback.Status == FeedbackStatus.Assigned ||
                report.Feedback.Status == FeedbackStatus.NeedRework)
            {
                statusHistory = await ChangeStatusAsync(
                    report.Feedback,
                    FeedbackStatus.InProgress,
                    currentUserId,
                    "Liên hệ coordinator thành công, bắt đầu xử lý.");
            }
        }

        await _uow.SaveAsync();

        if (statusHistory != null)
        {
            await SynchronizeSlaByStatusAsync(
                report.Feedback.FeedbackId,
                statusHistory.OldStatus!,
                statusHistory.NewStatus,
                currentUserId,
                statusHistory.Note);

            await SendStatusUpdatedNotificationAsync(
                report.Feedback,
                statusHistory);
        }
        await SendFeedbackNotificationAsync(
            report.Feedback,
            "Đã cập nhật liên hệ nhà cung cấp",
            $"Phản ánh \"{report.Feedback.Title}\" đã có thông tin liên hệ mới từ nhà cung cấp");

        var saved = await _uow.GetRepository<ProviderContactLog>().Entities
            .AsNoTracking()
            .Include(l => l.Coordinator)
            .Include(l => l.ContactedByUser)
            .FirstAsync(l => l.ContactLogId == log.ContactLogId);

        return MapContactLog(saved);
    }

    private static bool IsSuccessfulCoordinatorContact(string? contactResult)
    {
        if (string.IsNullOrWhiteSpace(contactResult))
        {
            return false;
        }

        var normalized = contactResult.Trim().ToLowerInvariant();

        if (normalized.Contains("liên hệ lại") ||
            normalized.Contains("lien he lai") ||
            normalized.Contains("cần gọi lại") ||
            normalized.Contains("can goi lai") ||
            normalized.Contains("không") ||
            normalized.Contains("khong") ||
            normalized.Contains("chưa") ||
            normalized.Contains("chua") ||
            normalized.Contains("thất bại") ||
            normalized.Contains("that bai") ||
            normalized.Contains("failed"))
        {
            return false;
        }

        return normalized.Contains("thành công") ||
            normalized.Contains("thanh cong") ||
            normalized.Contains("đã liên hệ") ||
            normalized.Contains("da lien he") ||
            normalized.Contains("successful") ||
            normalized.Contains("success");
    }

    public async Task<IReadOnlyCollection<ProviderContactLogDto>> GetProviderContactLogsAsync(int providerReportId)
    {
        await EnsureProviderReportExistsAsync(providerReportId);

        var logs = await _uow.GetRepository<ProviderContactLog>().Entities
            .AsNoTracking()
            .Include(l => l.Coordinator)
            .Include(l => l.ContactedByUser)
            .Where(l => l.ProviderReportId == providerReportId)
            .OrderByDescending(l => l.ContactedAt)
            .ToListAsync();

        return logs
            .Select(MapContactLog)
            .ToList();
    }

    public async Task<IReadOnlyCollection<CompletionDocumentDto>> AddCompletionDocumentsAsync(
        int providerReportId,
        Guid currentUserId,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> documents,
        string? description)
    {
        var report = await _uow.GetRepository<FeedbackProviderReport>().Entities
            .AsNoTracking()
            .Include(r => r.Feedback)
            .FirstOrDefaultAsync(r => r.ProviderReportId == providerReportId)
            ?? throw new Exception("Provider report khong ton tai.");

        var now = DateTime.UtcNow;
        foreach (var document in documents)
        {
            await _uow.GetRepository<CompletionDocument>().AddAsync(new CompletionDocument
            {
                ProviderReportId = providerReportId,
                FeedbackId = report.FeedbackId,
                CoordinatorId = report.CoordinatorId,
                UploadedByUserId = currentUserId,
                FileUrl = document.FileUrl,
                FileType = document.FileType,
                Description = NormalizeOptional(description),
                ReceivedAt = now
            });
        }

        await _uow.SaveAsync();
        await SendFeedbackNotificationAsync(
            report.Feedback,
            "Đã có tài liệu hoàn thành mới",
            $"Phản ánh \"{report.Feedback.Title}\" đã được cập nhật tài liệu hoàn thành.");

        return await GetCompletionDocumentsAsync(providerReportId);
    }

    public async Task<IReadOnlyCollection<CompletionDocumentDto>> GetCompletionDocumentsAsync(int providerReportId)
    {
        await EnsureProviderReportExistsAsync(providerReportId);

        var documents = await _uow.GetRepository<CompletionDocument>().Entities
            .AsNoTracking()
            .Include(d => d.Coordinator)
            .Include(d => d.UploadedByUser)
            .Where(d => d.ProviderReportId == providerReportId)
            .OrderByDescending(d => d.ReceivedAt)
            .ToListAsync();

        return documents
            .Select(MapCompletionDocument)
            .ToList();
    }

    public async Task<IReadOnlyCollection<FeedbackResolutionDto>> GetFeedbackResolutionsAsync(Guid feedbackId)
    {
        await EnsureFeedbackExistsAsync(feedbackId);

        var resolutions = await _uow.GetRepository<FeedbackResolution>().Entities
            .AsNoTracking()
            .Include(r => r.CreatedByStaffUser)
            .Where(r => r.FeedbackId == feedbackId)
            .OrderByDescending(r => r.ResolvedAt)
            .ToListAsync();

        return resolutions
            .Select(MapResolution)
            .ToList();
    }

    public async Task<FeedbackResolutionDto> GetResolutionAsync(int resolutionId)
    {
        var resolution = await _uow.GetRepository<FeedbackResolution>().Entities
            .AsNoTracking()
            .Include(r => r.CreatedByStaffUser)
            .FirstOrDefaultAsync(r => r.ResolutionId == resolutionId)
            ?? throw new Exception("Khong tim thay resolution.");

        return MapResolution(resolution);
    }

    public async Task NotifyProviderResultAsync(
        Guid feedbackId,
        NotifyProviderResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new Exception("Title la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new Exception("Message la bat buoc.");
        }

        var feedback = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId)
            ?? throw new Exception("Khong tim thay feedback.");

        await _notificationService.SendAsync(
            feedback.UserId,
            request.Title.Trim(),
            request.Message.Trim(),
            NotificationType.TicketUpdated,
            string.IsNullOrWhiteSpace(request.TargetUrl)
                ? $"/feedbacks/{feedbackId}"
                : request.TargetUrl.Trim());
    }

    public async Task SubmitResolutionAsync(
    SubmitResolutionRequest request)
    {
        var feedback =
            await GetFeedbackWithDetailsAsync(
                request.FeedbackId,
                false);

        FeedbackProviderReport? report = null;
        if (request.ProviderReportId.HasValue)
        {
            report = await _uow
                .GetRepository<FeedbackProviderReport>()
                .GetByIdAsync(request.ProviderReportId.Value);

            if (report == null || report.FeedbackId != request.FeedbackId)
            {
                throw new Exception("Provider report khong hop le.");
            }
        }

        if (feedback.Status != FeedbackStatus.InProgress &&
            feedback.Status != FeedbackStatus.NeedRework)
        {
            throw new Exception("Feedback must be InProgress or NeedRework before submitting resolution.");
        }

        if (report != null &&
            !string.Equals(report.ReportStatus, "InProgress", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(report.ReportStatus, "Done", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Provider report must be InProgress before submitting resolution.");
        }

        var resolution =
            new FeedbackResolution
            {
                FeedbackId =
                    request.FeedbackId,

                ProviderReportId =
                    request.ProviderReportId,

                CreatedByStaffUserId =
                    request.StaffUserId,

                ResolutionSummary =
                    request.ResolutionSummary,

                ActionTaken =
                    request.ActionTaken,

                ResultNote =
                    request.ResultNote,

                Status =
                    FeedbackStatus.SubmittedForApproval,

                ResolvedAt =
                    DateTime.UtcNow
            };

        await _uow
            .GetRepository<FeedbackResolution>()
            .AddAsync(resolution);

        if (report != null)
        {
            report.ReportStatus = "Done";
            report.UpdatedAt = DateTime.UtcNow;

            foreach (var image in request.ImageUrls)
            {
                await _uow
                    .GetRepository<CompletionDocument>()
                    .AddAsync(
                        new CompletionDocument
                        {
                            ProviderReportId = report.ProviderReportId,
                            FeedbackId = request.FeedbackId,
                            CoordinatorId = report.CoordinatorId,
                            UploadedByUserId = request.StaffUserId,
                            FileUrl = image,
                            FileType = "image",
                            ReceivedAt = DateTime.UtcNow
                        });
            }
        }

        var history = await ChangeStatusAsync(
            feedback,
            FeedbackStatus.SubmittedForApproval,
            request.StaffUserId);

        await _uow.SaveAsync();
        await SendStatusUpdatedNotificationAsync(feedback, history);
    }

    public async Task ApproveResolutionAsync(
    Guid feedbackId,
    Guid managerId,
    string? note)
    {
        var feedback = await GetFeedbackWithDetailsAsync(
            feedbackId,
            false);

        var resolution = (await _uow
                .GetRepository<FeedbackResolution>()
                .GetAllAsync(x =>
                    x.FeedbackId == feedbackId))
            .OrderByDescending(x => x.ResolvedAt)
            .FirstOrDefault()
            ?? throw new Exception(
                "Không tìm thấy resolution để phê duyệt.");

        if (feedback.Status != FeedbackStatus.SubmittedForApproval)
        {
            throw new Exception("Feedback must be SubmittedForApproval before approval.");
        }

        var oldStatus = feedback.Status;

        resolution.Status =
            FeedbackStatus.Approved;

        feedback.ApprovedByManagerId =
            managerId;

        feedback.ApprovedAt =
            DateTime.UtcNow;

        var history = await ChangeStatusAsync(
            feedback,
            FeedbackStatus.Approved,
            managerId,
            note);

        await _uow.SaveAsync();

        // Approved là thời điểm manager xác nhận việc xử lý đã hoàn tất.
        // SLA resolution được hoàn thành tại đây, không chờ citizen review.
        await SynchronizeSlaByStatusAsync(
            feedback.FeedbackId,
            oldStatus,
            feedback.Status,
            managerId,
            note);

        await SendStatusUpdatedNotificationAsync(
            feedback,
            history);
    }


    public async Task RequireReworkAsync(
    Guid feedbackId,
    Guid managerId,
    string reason)
    {
        var feedback =
            await GetFeedbackWithDetailsAsync(
                feedbackId,
                false);

        var resolution =
            (await _uow
                .GetRepository<FeedbackResolution>()
                .GetAllAsync(
                    x => x.FeedbackId ==
                         feedbackId))
            .OrderByDescending(
                x => x.ResolvedAt)
            .First();

        resolution.Status =
            FeedbackStatus.NeedRework;

        if (feedback.Status != FeedbackStatus.SubmittedForApproval)
        {
            throw new Exception("Feedback must be SubmittedForApproval before requiring rework.");
        }

        resolution.ResultNote =
            reason;

        var latestProviderReport = await _uow
            .GetRepository<FeedbackProviderReport>()
            .Entities
            .Where(x => x.FeedbackId == feedbackId)
            .OrderByDescending(x => x.ReportedAt)
            .FirstOrDefaultAsync();

        if (latestProviderReport != null &&
            string.Equals(latestProviderReport.ReportStatus, "Done", StringComparison.OrdinalIgnoreCase))
        {
            latestProviderReport.ReportStatus = "InProgress";
            latestProviderReport.UpdatedAt = DateTime.UtcNow;
        }

        var history = await ChangeStatusAsync(
            feedback,
            FeedbackStatus.NeedRework,
            managerId,
            reason);

        await _uow.SaveAsync();
        await SendStatusUpdatedNotificationAsync(feedback, history);
    }

    public async Task<FeedbackResolutionReviewDto> CitizenReviewAsync(
    CitizenReviewRequest request)
    {
        var feedback =
            await GetFeedbackWithDetailsAsync(
                request.FeedbackId,
                false);

        if (feedback.UserId != request.UserId)
        {
            throw new Exception("Chi chu so huu feedback moi duoc danh gia ket qua.");
        }

        if (feedback.Status !=
            FeedbackStatus.Approved)
            throw new Exception(
                "Feedback must be Approved.");

        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new Exception("Rating phai nam trong khoang 1 den 5.");
        }

        var review =
            new FeedbackResolutionReview
            {
                FeedbackId =
                    request.FeedbackId,

                UserId =
                    request.UserId,

                Rating =
                    request.Rating,

                IsSatisfied =
                    request.IsSatisfied,

                Comment =
                    request.Comment ?? string.Empty,

                CreatedAt =
                    DateTime.UtcNow
            };

        await _uow
            .GetRepository<FeedbackResolutionReview>()
            .AddAsync(review);

        var history = await ChangeStatusAsync(
            feedback,
            FeedbackStatus.Closed,
            request.UserId);

        await _uow.SaveAsync();
        await SendStatusUpdatedNotificationAsync(feedback, history);

        var saved = await _uow.GetRepository<FeedbackResolutionReview>().Entities
            .AsNoTracking()
            .Include(r => r.User)
            .FirstAsync(r => r.ReviewId == review.ReviewId);

        return MapResolutionReview(saved);
    }

    private async Task SynchronizeSlaByStatusAsync(
    Guid feedbackId,
    string oldStatus,
    string newStatus,
    Guid triggeredByUserId,
    string? note)
    {
        if (string.Equals(
                oldStatus,
                newStatus,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Verified: bắt đầu SLA.
        if (string.Equals(
                newStatus,
                FeedbackStatus.Verified,
                StringComparison.OrdinalIgnoreCase))
        {
            var hasCurrentSla = await _uow
                .GetRepository<FeedbackSla>()
                .Entities
                .AsNoTracking()
                .AnyAsync(x =>
                    x.FeedbackId == feedbackId &&
                    x.IsCurrent);

            if (!hasCurrentSla)
            {
                await _slaService.StartAsync(
                    feedbackId,
                    triggeredByUserId);
            }

            return;
        }

        // InProgress: phản hồi đầu tiên được ghi nhận.
        if (string.Equals(
                newStatus,
                FeedbackStatus.InProgress,
                StringComparison.OrdinalIgnoreCase))
        {
            var currentSla = await _uow
                .GetRepository<FeedbackSla>()
                .Entities
                .AsNoTracking()
                .Where(x =>
                    x.FeedbackId == feedbackId &&
                    x.IsCurrent)
                .Select(x => new
                {
                    x.RespondedAt,
                    x.Status
                })
                .FirstOrDefaultAsync();

            if (currentSla != null &&
                !currentSla.RespondedAt.HasValue &&
                string.Equals(
                    currentSla.Status,
                    SlaStatus.Running,
                    StringComparison.OrdinalIgnoreCase))
            {
                await _slaService.MarkRespondedAsync(
                    feedbackId,
                    triggeredByUserId,
                    NormalizeOptional(note) ??
                    "Feedback bắt đầu được xử lý.");
            }

            return;
        }

        // Approved: manager xác nhận kết quả xử lý, hoàn thành SLA.
        if (string.Equals(
                newStatus,
                FeedbackStatus.Approved,
                StringComparison.OrdinalIgnoreCase))
        {
            var currentSla = await _uow
                .GetRepository<FeedbackSla>()
                .Entities
                .AsNoTracking()
                .Where(x =>
                    x.FeedbackId == feedbackId &&
                    x.IsCurrent)
                .Select(x => new
                {
                    x.Status
                })
                .FirstOrDefaultAsync();

            if (currentSla != null &&
                !string.Equals(
                    currentSla.Status,
                    SlaStatus.Completed,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    currentSla.Status,
                    SlaStatus.Cancelled,
                    StringComparison.OrdinalIgnoreCase))
            {
                await _slaService.CompleteAsync(
                    feedbackId,
                    triggeredByUserId,
                    new CompleteSlaRequest
                    {
                        Note = NormalizeOptional(note) ??
                            "Manager đã xác nhận kết quả xử lý feedback."
                    });
            }
        }
    }
}
