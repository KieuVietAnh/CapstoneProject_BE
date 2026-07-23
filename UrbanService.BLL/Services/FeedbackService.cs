using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using UrbanService.DAL.UnitOfWork;

namespace UrbanService.BLL.Services;

public class FeedbackService : IFeedbackService
{
    private const int MaxPageSize = 100;
    private static readonly IReadOnlyCollection<string> AllowedProviderReportStatuses =
    [
        "Reported",
        "Contacted",
        "Accepted",
        "InProgress",
        "Done",
        "Failed",
        "Cancelled"
    ];

    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly IAiFeedbackReviewQueue _aiFeedbackReviewQueue;
    private readonly IAiFeedbackDuplicateService _aiFeedbackDuplicateService;

    public FeedbackService(
        IUnitOfWork uow,
        INotificationService notificationService,
        IAiFeedbackReviewQueue aiFeedbackReviewQueue,
        IAiFeedbackDuplicateService aiFeedbackDuplicateService)
    {
        _uow = uow;
        _notificationService = notificationService;
        _aiFeedbackReviewQueue = aiFeedbackReviewQueue;
        _aiFeedbackDuplicateService = aiFeedbackDuplicateService;
    }

    public async Task<FeedbackDetailDto> CreateAsync(
        Guid userId,
        FeedbackCreateRequest request,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments)
    {
        ValidateCreate(request);
        await EnsureAreaExistsAsync(request.AreaId);

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
                SupportCount = f.FeedbackSupports.Count
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
                SupportCount = f.FeedbackSupports.Count
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

        var feedbacks = _uow.GetRepository<Feedback>().Entities.AsNoTracking();

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
                SupportCount = f.FeedbackSupports.Count
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
                    SupportCount = f.FeedbackSupports.Count
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

        if (request.AreaId.HasValue && request.AreaId.Value != feedback.AreaId)
        {
            await EnsureAreaExistsAsync(request.AreaId.Value);
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
        var feedback = await GetFeedbackWithDetailsAsync(feedbackId, asNoTracking: false);
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

        if (request.AreaId.HasValue && request.AreaId.Value != feedback.AreaId)
        {
            await EnsureAreaExistsAsync(request.AreaId.Value);
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
        feedback.IsLocationVerified = true;
        feedback.Priority = string.IsNullOrWhiteSpace(request.Priority) ? feedback.Priority : request.Priority.Trim();
        feedback.DueDate = request.DueDate ?? feedback.DueDate;
        feedback.UpdatedAt = DateTime.UtcNow;

        FeedbackStatusHistory? statusHistory = null;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !string.Equals(feedback.Status, request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var newStatus = FeedbackStatus.Normalize(request.Status);
            statusHistory = new FeedbackStatusHistory
            {
                FeedbackId = feedbackId,
                ChangedByUserId = currentUserId,
                OldStatus = feedback.Status,
                NewStatus = newStatus,
                Note = request.StatusNote?.Trim(),
                ChangedAt = DateTime.UtcNow
            };

            feedback.Status = statusHistory.NewStatus;
            feedback.FeedbackStatusHistories.Add(statusHistory);
        }

        await _uow.SaveAsync();

        if (statusHistory != null)
        {
            await SendStatusUpdatedNotificationAsync(feedback, statusHistory);
        }

        if (hasContentChanges)
        {
            await SendFeedbackNotificationAsync(
                feedback,
                "Phản ánh đã được nhân viên cập nhật",
                $"Thông tin phản ánh \"{feedback.Title}\" đã được nhân viên cập nhật.");
        }

        return await GetFeedbackDetailAsync(currentUserId, feedbackId);
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

    public async Task<FeedbackStatusHistoryDto> UpdateStatusByStaffOrAdminAsync(
        Guid currentUserId,
        Guid feedbackId,
        UpdateFeedbackStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new Exception("Status là bắt buộc.");
        }

        var feedback = await GetFeedbackWithDetailsAsync(feedbackId, asNoTracking: false);
        var newStatus = FeedbackStatus.Normalize(request.Status);

        if (string.Equals(feedback.Status, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Feedback đã ở trạng thái {newStatus}.");
        }

        var now = DateTime.UtcNow;

        var history = new FeedbackStatusHistory
        {
            FeedbackId = feedbackId,
            ChangedByUserId = currentUserId,
            OldStatus = feedback.Status,
            NewStatus = newStatus,
            Note = request.Note?.Trim(),
            ChangedAt = now
        };

        feedback.Status = newStatus;
        feedback.UpdatedAt = now;
        feedback.FeedbackStatusHistories.Add(history);

        await _uow.SaveAsync();

        await SendStatusUpdatedNotificationAsync(feedback, history);

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
        await _notificationService.SendAsync(
            feedback.UserId,
            "Trạng thái phản ánh đã được cập nhật",
            $"Phản ánh \"{feedback.Title}\" đã chuyển trạng thái từ \"{history.OldStatus}\" sang \"{history.NewStatus}\".",
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
        dto.ParentTicketId = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Where(feedback => feedback.FeedbackId == dto.FeedbackId)
            .Select(feedback => feedback.ParentTicketId)
            .FirstOrDefaultAsync();

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

        feedback.Status = FeedbackStatus.Normalize(newStatus);
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
        var feedback =
            await GetFeedbackWithDetailsAsync(
                feedbackId,
                false);

        if (feedback.Status != FeedbackStatus.Submitted &&
            feedback.Status != FeedbackStatus.AiReviewed)
            throw new Exception(
                "Feedback must be Submitted or AiReviewed.");

        var history = await ChangeStatusAsync(
            feedback,
            FeedbackStatus.Verified,
            staffUserId,
            "Verified by staff");

        await _uow.SaveAsync();
        await SendStatusUpdatedNotificationAsync(feedback, history);
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
            report.Feedback.Status == FeedbackStatus.Assigned)
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
            await SendStatusUpdatedNotificationAsync(report.Feedback, statusHistory);
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

        if (string.Equals(report.ReportStatus, "Reported", StringComparison.OrdinalIgnoreCase))
        {
            report.ReportStatus = "Contacted";
            report.UpdatedAt = now;
        }

        await _uow.SaveAsync();
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
        await SendStatusUpdatedNotificationAsync(feedback, history);
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

        resolution.ResultNote =
            reason;

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
}
