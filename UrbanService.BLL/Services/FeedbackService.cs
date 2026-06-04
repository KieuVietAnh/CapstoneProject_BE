using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class FeedbackService : IFeedbackService
{
    private const int MaxPageSize = 100;
    private readonly IUnitOfWork _uow;

    public FeedbackService(IUnitOfWork uow)
    {
        _uow = uow;
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
            Status = "Submitted",
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

    public async Task<FeedbackStatusHistoryDto> UpdateStatusAsync(Guid userId, Guid feedbackId, UpdateFeedbackStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new Exception("Status là bắt buộc.");
        }

        var feedback = await GetOwnedFeedbackWithDetailsAsync(userId, feedbackId, asNoTracking: false);
        var newStatus = request.Status.Trim();
        var now = DateTime.UtcNow;

        var history = new FeedbackStatusHistory
        {
            FeedbackId = feedbackId,
            ChangedByUserId = userId,
            OldStatus = feedback.Status,
            NewStatus = newStatus,
            Note = request.Note?.Trim(),
            ChangedAt = now
        };

        feedback.Status = newStatus;
        feedback.UpdatedAt = now;
        feedback.FeedbackStatusHistories.Add(history);

        await _uow.SaveAsync();

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

    private async Task<Feedback> GetOwnedFeedbackWithDetailsAsync(Guid userId, Guid feedbackId, bool asNoTracking)
    {
        IQueryable<Feedback> query = _uow.GetRepository<Feedback>().Entities;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        var feedback = await query
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
}
