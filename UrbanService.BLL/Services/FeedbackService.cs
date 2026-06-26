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
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notificationService;
    private readonly IAiFeedbackReviewQueue _aiFeedbackReviewQueue;

    public FeedbackService(
        IUnitOfWork uow,
        INotificationService notificationService,
        IAiFeedbackReviewQueue aiFeedbackReviewQueue)
    {
        _uow = uow;
        _notificationService = notificationService;
        _aiFeedbackReviewQueue = aiFeedbackReviewQueue;
    }

    public async Task<FeedbackDetailDto> CreateAsync(
        Guid userId,
        FeedbackCreateRequest request,
        IReadOnlyCollection<UploadedFeedbackAttachmentDto> attachments)
    {
        ValidateCreate(request);
        await EnsureCategoryExistsAsync(request.CategoryId);

        var now = DateTime.UtcNow;
        var feedback = new Feedback
        {
            FeedbackId = Guid.NewGuid(),
            UserId = userId,
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            LocationText = request.LocationText.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Priority = NormalizeOrDefault(request.Priority, "Medium"),
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

        await _aiFeedbackReviewQueue.EnqueueAsync(feedback.FeedbackId, userId);

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
        return MapDetail(feedback, userId);
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
        return MapDetail(feedback, currentUserId);
    }

    public async Task<FeedbackDetailDto> UpdateAsync(Guid userId, Guid feedbackId, FeedbackUpdateRequest request)
    {
        var feedback = await GetOwnedFeedbackWithDetailsAsync(userId, feedbackId, asNoTracking: false);

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
        feedback.Priority = string.IsNullOrWhiteSpace(request.Priority) ? feedback.Priority : request.Priority.Trim();
        feedback.DueDate = request.DueDate ?? feedback.DueDate;
        feedback.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveAsync();
        return await GetMyFeedbackDetailAsync(userId, feedbackId);
    }

    public async Task<FeedbackDetailDto> UpdateByStaffAsync(
        Guid currentUserId,
        Guid feedbackId,
        StaffFeedbackUpdateRequest request)
    {
        var feedback = await GetFeedbackWithDetailsAsync(feedbackId, asNoTracking: false);

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

    private async Task<Feedback> GetFeedbackWithDetailsAsync(Guid feedbackId, bool asNoTracking)
    {
        IQueryable<Feedback> query = _uow.GetRepository<Feedback>().Entities;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        var feedback = await query
            .Include(f => f.User)
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
            "Trạng thái feedback đã được cập nhật",
            $"Feedback \"{feedback.Title}\" đã chuyển từ \"{history.OldStatus}\" sang \"{history.NewStatus}\".",
            NotificationType.TicketUpdated,
            $"/feedbacks/{feedback.FeedbackId}");
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

    private static FeedbackDetailDto MapDetail(Feedback feedback, Guid userId)
    {
        return new FeedbackDetailDto
        {
            FeedbackId = feedback.FeedbackId,
            UserId = feedback.UserId,
            UserName = feedback.User?.FullName,
            CategoryId = feedback.CategoryId,
            CategoryName = feedback.Category?.CategoryName,
            Title = feedback.Title,
            Description = feedback.Description,
            LocationText = feedback.LocationText,
            Latitude = feedback.Latitude,
            Longitude = feedback.Longitude,
            Priority = feedback.Priority,
            Status = feedback.Status,
            DueDate = feedback.DueDate,
            CreatedAt = feedback.CreatedAt,
            UpdatedAt = feedback.UpdatedAt,
            AttachmentCount = feedback.FeedbackAttachments.Count,
            CommentCount = feedback.FeedbackComments.Count,
            SupportCount = feedback.FeedbackSupports.Count,
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
        if (request.CategoryId <= 0)
        {
            throw new Exception("CategoryId là bắt buộc.");
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

    private async Task ChangeStatusAsync(
    Feedback feedback,
    string newStatus,
    Guid userId,
    string? note = null)
    {
        var oldStatus = feedback.Status;

        feedback.Status = FeedbackStatus.Normalize(newStatus);
        feedback.UpdatedAt = DateTime.UtcNow;

        await _uow
            .GetRepository<FeedbackStatusHistory>()
            .AddAsync(new FeedbackStatusHistory
            {
                FeedbackId = feedback.FeedbackId,
                ChangedByUserId = userId,
                OldStatus = oldStatus,
                NewStatus = feedback.Status,
                Note = note,
                ChangedAt = DateTime.UtcNow
            });
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

        await ChangeStatusAsync(
            feedback,
            FeedbackStatus.Verified,
            staffUserId,
            "Verified by staff");

        await _uow.SaveAsync();
    }

    public async Task AssignFeedbackAsync(
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

            await _uow
                .GetRepository<FeedbackAssignment>()
                .AddAsync(
                    new FeedbackAssignment
                    {
                        FeedbackId =
                            request.FeedbackId,

                        OperatorId =
                            request.OperatorId,

                        AssignedByUserId =
                            request.StaffUserId,

                        AssignmentStatus =
                            "Assigned",

                        Note =
                            request.Note,

                        AssignedAt =
                            DateTime.UtcNow
                    });

            await ChangeStatusAsync(
                feedback,
                FeedbackStatus.Assigned,
                request.StaffUserId);

            await _uow.SaveAsync();

            _uow.CommitTransaction();
        }
        catch
        {
            _uow.RollBack();
            throw;
        }
    }

    public async Task SubmitResolutionAsync(
    SubmitResolutionRequest request)
    {
        var feedback =
            await GetFeedbackWithDetailsAsync(
                request.FeedbackId,
                false);

        var user =
            await _uow.GetRepository<User>()
                .GetByIdAsync(
                    request.OperatorUserId);

        var resolution =
            new FeedbackResolution
            {
                FeedbackId =
                    request.FeedbackId,

                OperatorId =
                    user!.OperatorId!.Value,

                ResolvedByUserId =
                    request.OperatorUserId,

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

        await _uow.SaveAsync();

        foreach (var image in request.ImageUrls)
        {
            await _uow
                .GetRepository<
                    FeedbackResolutionAttachment>()
                .AddAsync(
                    new FeedbackResolutionAttachment
                    {
                        ResolutionId =
                            resolution.ResolutionId,

                        FileUrl = image,

                        FileType = "image",

                        UploadedAt =
                            DateTime.UtcNow
                    });
        }

        await ChangeStatusAsync(
            feedback,
            FeedbackStatus.SubmittedForApproval,
            request.OperatorUserId);

        await _uow.SaveAsync();
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

        await ChangeStatusAsync(
            feedback,
            FeedbackStatus.Approved,
            managerId,
            note);

        await _uow.SaveAsync();
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

        await ChangeStatusAsync(
            feedback,
            FeedbackStatus.NeedRework,
            managerId,
            reason);

        await _uow.SaveAsync();
    }

    public async Task CitizenReviewAsync(
    CitizenReviewRequest request)
    {
        var feedback =
            await GetFeedbackWithDetailsAsync(
                request.FeedbackId,
                false);

        if (feedback.Status !=
            FeedbackStatus.Approved)
            throw new Exception(
                "Feedback must be Approved.");

        await _uow
            .GetRepository<
                FeedbackResolutionReview>()
            .AddAsync(
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
                        request.Comment,

                    CreatedAt =
                        DateTime.UtcNow
                });

        await ChangeStatusAsync(
            feedback,
            FeedbackStatus.Closed,
            request.UserId);

        await _uow.SaveAsync();
    }
}
