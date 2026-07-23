using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class FeedbackDuplicateCandidateService : IFeedbackDuplicateCandidateService
{
    private const int MaxPageSize = 100;
    private const string PendingStatus = "Pending";
    private const string ConfirmedStatus = "Confirmed";
    private const string RejectedStatus = "Rejected";

    private readonly IUnitOfWork _uow;

    public FeedbackDuplicateCandidateService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<FeedbackDuplicateSummaryDto> GetSummaryAsync()
    {
        var candidates = _uow.GetRepository<FeedbackDuplicateCandidate>().Entities.AsNoTracking();

        return new FeedbackDuplicateSummaryDto
        {
            PendingCount = await candidates.CountAsync(c => c.Status == PendingStatus),
            ConfirmedCount = await candidates.CountAsync(c => c.Status == ConfirmedStatus),
            RejectedCount = await candidates.CountAsync(c => c.Status == RejectedStatus),
            TotalCount = await candidates.CountAsync()
        };
    }

    public async Task<PagedResultDto<FeedbackDuplicateCandidateDto>> GetCandidatesAsync(FeedbackDuplicateQueryParameters query)
    {
        var pageNumber = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var status = query.Status?.Trim();

        var candidates = BaseCandidateQuery();

        if (!string.IsNullOrWhiteSpace(status))
        {
            candidates = candidates.Where(c => c.Status.ToLower() == status.ToLower());
        }

        var totalItems = await candidates.CountAsync();
        var rows = await candidates
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<FeedbackDuplicateCandidateDto>
        {
            Items = rows.Select(MapCandidate).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<FeedbackDuplicateCandidateDto> GetCandidateDetailAsync(Guid duplicateCandidateId)
    {
        var candidate = await BaseCandidateQuery()
            .FirstOrDefaultAsync(c => c.DuplicateCandidateId == duplicateCandidateId)
            ?? throw new Exception("Không tìm thấy duplicate candidate.");

        return MapCandidate(candidate);
    }

    public async Task<FeedbackDuplicateCandidateDto> ConfirmAsync(Guid duplicateCandidateId, Guid staffUserId)
    {
        var candidate = await _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .Include(c => c.Feedback)
            .Include(c => c.PotentialParentFeedback)
            .FirstOrDefaultAsync(c => c.DuplicateCandidateId == duplicateCandidateId)
            ?? throw new Exception("Không tìm thấy duplicate candidate.");

        if (!string.Equals(candidate.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Duplicate candidate không ở trạng thái Pending.");
        }

        if (candidate.FeedbackId == candidate.PotentialParentFeedbackId)
        {
            throw new Exception("Feedback con và feedback chính không hợp lệ.");
        }

        var childFeedback = candidate.Feedback;
        var parentFeedback = candidate.PotentialParentFeedback;

        childFeedback.ParentTicketId = parentFeedback.FeedbackId;
        childFeedback.IsMasterTicket = false;
        childFeedback.UpdatedAt = DateTime.UtcNow;

        parentFeedback.IsMasterTicket = true;
        parentFeedback.UpdatedAt = DateTime.UtcNow;

        candidate.Status = ConfirmedStatus;
        candidate.ReviewedByUserId = staffUserId;
        candidate.ReviewedAt = DateTime.UtcNow;
        candidate.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveAsync();

        return await GetCandidateDetailAsync(duplicateCandidateId);
    }

    public async Task<FeedbackDuplicateCandidateDto> RejectAsync(Guid duplicateCandidateId, Guid staffUserId)
    {
        var candidate = await _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .FirstOrDefaultAsync(c => c.DuplicateCandidateId == duplicateCandidateId)
            ?? throw new Exception("Không tìm thấy duplicate candidate.");

        if (!string.Equals(candidate.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Duplicate candidate không ở trạng thái Pending.");
        }

        candidate.Status = RejectedStatus;
        candidate.ReviewedByUserId = staffUserId;
        candidate.ReviewedAt = DateTime.UtcNow;
        candidate.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveAsync();

        return await GetCandidateDetailAsync(duplicateCandidateId);
    }

    public async Task<IReadOnlyCollection<FeedbackListItemDto>> GetLinkedFeedbacksAsync(Guid feedbackId)
    {
        await EnsureFeedbackExistsAsync(feedbackId);

        var linkedFeedbacks = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.Area)
            .Include(f => f.Category)
            .Include(f => f.FeedbackAttachments)
            .Include(f => f.FeedbackComments)
            .Include(f => f.FeedbackSupports)
            .Where(f => f.ParentTicketId == feedbackId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return linkedFeedbacks.Select(MapFeedbackListItem).ToList();
    }

    public async Task<RelatedFeedbacksDto> GetRelatedFeedbacksAsync(Guid feedbackId)
    {
        var feedback = await _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FeedbackId == feedbackId)
            ?? throw new Exception("Không tìm thấy feedback.");

        var masterFeedbackId = feedback.ParentTicketId ?? feedback.FeedbackId;

        var masterFeedback = await FeedbackListQuery()
            .FirstOrDefaultAsync(f => f.FeedbackId == masterFeedbackId)
            ?? throw new Exception("Không tìm thấy ticket chính.");

        var linkedFeedbacks = await FeedbackListQuery()
            .Where(f => f.ParentTicketId == masterFeedbackId && f.FeedbackId != feedbackId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return new RelatedFeedbacksDto
        {
            FeedbackId = feedbackId,
            MasterFeedbackId = masterFeedbackId,
            MasterFeedback = MapFeedbackListItem(masterFeedback),
            LinkedFeedbacks = linkedFeedbacks.Select(MapFeedbackListItem).ToList()
        };
    }

    private IQueryable<FeedbackDuplicateCandidate> BaseCandidateQuery()
    {
        return _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .AsNoTracking()
            .Include(c => c.Feedback)
                .ThenInclude(f => f.User)
            .Include(c => c.Feedback)
                .ThenInclude(f => f.Area)
            .Include(c => c.Feedback)
                .ThenInclude(f => f.Category)
            .Include(c => c.Feedback)
                .ThenInclude(f => f.FeedbackAttachments)
            .Include(c => c.Feedback)
                .ThenInclude(f => f.FeedbackComments)
            .Include(c => c.Feedback)
                .ThenInclude(f => f.FeedbackSupports)
            .Include(c => c.PotentialParentFeedback)
                .ThenInclude(f => f.User)
            .Include(c => c.PotentialParentFeedback)
                .ThenInclude(f => f.Area)
            .Include(c => c.PotentialParentFeedback)
                .ThenInclude(f => f.Category)
            .Include(c => c.PotentialParentFeedback)
                .ThenInclude(f => f.FeedbackAttachments)
            .Include(c => c.PotentialParentFeedback)
                .ThenInclude(f => f.FeedbackComments)
            .Include(c => c.PotentialParentFeedback)
                .ThenInclude(f => f.FeedbackSupports)
            .Include(c => c.ReviewedByUser);
    }

    private IQueryable<Feedback> FeedbackListQuery()
    {
        return _uow.GetRepository<Feedback>().Entities
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.Area)
            .Include(f => f.Category)
            .Include(f => f.FeedbackAttachments)
            .Include(f => f.FeedbackComments)
            .Include(f => f.FeedbackSupports);
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

    private static FeedbackDuplicateCandidateDto MapCandidate(FeedbackDuplicateCandidate candidate)
    {
        return new FeedbackDuplicateCandidateDto
        {
            DuplicateCandidateId = candidate.DuplicateCandidateId,
            FeedbackId = candidate.FeedbackId,
            PotentialParentFeedbackId = candidate.PotentialParentFeedbackId,
            Status = candidate.Status,
            ConfidenceScore = candidate.ConfidenceScore,
            Reason = candidate.Reason,
            ReviewedByUserId = candidate.ReviewedByUserId,
            ReviewedByUserName = candidate.ReviewedByUser?.FullName,
            CreatedAt = candidate.CreatedAt,
            ReviewedAt = candidate.ReviewedAt,
            UpdatedAt = candidate.UpdatedAt,
            Feedback = MapFeedbackListItem(candidate.Feedback),
            PotentialParentFeedback = MapFeedbackListItem(candidate.PotentialParentFeedback)
        };
    }

    private static FeedbackListItemDto MapFeedbackListItem(Feedback feedback)
    {
        return new FeedbackListItemDto
        {
            FeedbackId = feedback.FeedbackId,
            UserId = feedback.UserId,
            UserName = feedback.User?.FullName,
            AreaId = feedback.AreaId,
            AreaName = feedback.Area?.AreaName,
            CategoryId = feedback.CategoryId,
            CategoryName = feedback.Category?.CategoryName,
            Title = feedback.Title,
            LocationText = feedback.LocationText,
            Priority = feedback.Priority,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt,
            UpdatedAt = feedback.UpdatedAt,
            AttachmentCount = feedback.FeedbackAttachments.Count,
            CommentCount = feedback.FeedbackComments.Count,
            SupportCount = feedback.FeedbackSupports.Count,
            DuplicateWarning = false,
            PotentialDuplicate = null,
            ParentTicketId = feedback.ParentTicketId
        };
    }
}