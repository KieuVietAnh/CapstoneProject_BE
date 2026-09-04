using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Constraint;
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
    private readonly INotificationService _notificationService;
    private readonly IIncidentService _incidentService;

    public FeedbackDuplicateCandidateService(
        IUnitOfWork uow,
        INotificationService notificationService,
        IIncidentService incidentService)
    {
        _uow = uow;
        _notificationService = notificationService;
        _incidentService = incidentService;
    }

    public async Task<FeedbackDuplicateSummaryDto> GetSummaryAsync(Guid actorUserId)
    {
        var actor = await GetDuplicateReadActorAsync(actorUserId);
        var candidates = ApplyCandidateReadScope(
            _uow.GetRepository<FeedbackDuplicateCandidate>().Entities.AsNoTracking(),
            actor);

        return new FeedbackDuplicateSummaryDto
        {
            PendingCount = await candidates.CountAsync(c => c.Status == PendingStatus),
            ConfirmedCount = await candidates.CountAsync(c => c.Status == ConfirmedStatus),
            RejectedCount = await candidates.CountAsync(c => c.Status == RejectedStatus),
            TotalCount = await candidates.CountAsync()
        };
    }

    public async Task<PagedResultDto<FeedbackDuplicateCandidateDto>> GetCandidatesAsync(
        FeedbackDuplicateQueryParameters query,
        Guid actorUserId)
    {
        var actor = await GetDuplicateReadActorAsync(actorUserId);
        var pageNumber = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var status = query.Status?.Trim();

        var candidates = ApplyCandidateReadScope(BaseCandidateQuery(), actor);

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

    public async Task<FeedbackDuplicateCandidateDto> GetCandidateDetailAsync(
        Guid duplicateCandidateId,
        Guid actorUserId)
    {
        var actor = await GetDuplicateReadActorAsync(actorUserId);
        var candidate = await ApplyCandidateReadScope(BaseCandidateQuery(), actor)
            .FirstOrDefaultAsync(c => c.DuplicateCandidateId == duplicateCandidateId)
            ?? throw new UrbanService.BLL.Common.ForbiddenAccessException(
                "Bạn không có quyền xem đề xuất trùng này.");

        return MapCandidate(candidate);
    }

    public async Task<FeedbackDuplicateCandidateDto> ConfirmAsync(
        Guid duplicateCandidateId,
        Guid reviewerUserId)
    {
        var candidateScope = await _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .AsNoTracking()
            .Where(candidate => candidate.DuplicateCandidateId == duplicateCandidateId)
            .Select(candidate => new
            {
                candidate.FeedbackId,
                candidate.PotentialParentFeedbackId
            })
            .SingleOrDefaultAsync()
            ?? throw new Exception("Không tìm thấy đề xuất phản ánh trùng.");
        await ManagementAccessRules.EnsureManagerFeedbackReviewAccessAsync(
            _uow,
            candidateScope.FeedbackId,
            reviewerUserId,
            requirePrimaryWhenLinked: false);
        await ManagementAccessRules.EnsureManagerFeedbackReviewAccessAsync(
            _uow,
            candidateScope.PotentialParentFeedbackId,
            reviewerUserId,
            requirePrimaryWhenLinked: false);
        Feedback childFeedback;
        Feedback parentFeedback;
        Guid? confirmedIncidentId = null;

        _uow.BeginTransaction();
        try
        {
            var candidateRepository = _uow.GetRepository<FeedbackDuplicateCandidate>();
            var feedbackRepository = _uow.GetRepository<Feedback>();

            var candidate = await candidateRepository.Entities
                .Include(c => c.Feedback)
                .Include(c => c.PotentialParentFeedback)
                .FirstOrDefaultAsync(c => c.DuplicateCandidateId == duplicateCandidateId)
                ?? throw new Exception("Không tìm thấy đề xuất phản ánh trùng.");

            if (string.Equals(candidate.Status, ConfirmedStatus, StringComparison.OrdinalIgnoreCase))
            {
                _uow.CommitTransaction();
                return await GetCandidateDetailCoreAsync(duplicateCandidateId);
            }

            if (!string.Equals(candidate.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Chỉ có thể xác nhận đề xuất đang ở trạng thái Pending.");
            }

            if (candidate.FeedbackId == candidate.PotentialParentFeedbackId)
            {
                throw new Exception("Phản ánh không thể là phản ánh cha của chính nó.");
            }

            childFeedback = candidate.Feedback;
            parentFeedback = candidate.PotentialParentFeedback;

            if (childFeedback.ParentTicketId.HasValue)
            {
                throw new Exception("Phản ánh này đã được liên kết với một phản ánh cha khác.");
            }

            if (!parentFeedback.IsMasterTicket || parentFeedback.ParentTicketId.HasValue)
            {
                throw new Exception("Phản ánh được chọn không còn là phản ánh cha hợp lệ.");
            }

            if (parentFeedback.AreaId != childFeedback.AreaId)
            {
                throw new Exception("Phản ánh cha và phản ánh trùng phải thuộc cùng một khu vực.");
            }

            if (!FeedbackStatus.IsEligibleDuplicateMasterStatus(parentFeedback.Status))
            {
                throw new Exception("Phản ánh cha chưa được công khai hoặc không còn ở trạng thái hợp lệ.");
            }

            if (parentFeedback.CreatedAt > childFeedback.CreatedAt ||
                (parentFeedback.CreatedAt == childFeedback.CreatedAt &&
                 string.CompareOrdinal(
                     parentFeedback.FeedbackId.ToString("D"),
                     childFeedback.FeedbackId.ToString("D")) >= 0))
            {
                throw new Exception("Phản ánh cha phải được tạo trước phản ánh trùng.");
            }

            var childHasLinkedFeedbacks = await feedbackRepository.Entities
                .AnyAsync(feedback => feedback.ParentTicketId == childFeedback.FeedbackId);

            if (childHasLinkedFeedbacks)
            {
                throw new Exception("Phản ánh đang có phản ánh con nên không thể chuyển thành phản ánh trùng.");
            }

            var competingCandidates = await candidateRepository.Entities
                .Where(other =>
                    other.FeedbackId == childFeedback.FeedbackId
                    && other.DuplicateCandidateId != candidate.DuplicateCandidateId
                    && (other.Status == PendingStatus || other.Status == ConfirmedStatus))
                .ToListAsync();

            if (competingCandidates.Any(other =>
                    string.Equals(other.Status, ConfirmedStatus, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("Phản ánh này đã có một liên kết trùng được xác nhận.");
            }

            var reviewedAt = DateTime.UtcNow;

            foreach (var competingCandidate in competingCandidates)
            {
                competingCandidate.Status = RejectedStatus;
                competingCandidate.ReviewedByUserId = reviewerUserId;
                competingCandidate.ReviewedAt = reviewedAt;
                competingCandidate.UpdatedAt = reviewedAt;
            }

            childFeedback.ParentTicketId = parentFeedback.FeedbackId;
            childFeedback.IsMasterTicket = false;
            childFeedback.UpdatedAt = reviewedAt;

            parentFeedback.IsMasterTicket = true;
            parentFeedback.UpdatedAt = reviewedAt;

            candidate.Status = ConfirmedStatus;
            candidate.ReviewedByUserId = reviewerUserId;
            candidate.ReviewedAt = reviewedAt;
            candidate.UpdatedAt = reviewedAt;

            var linkedIncidentId = await _incidentService.RelinkConfirmedDuplicateAsync(
                childFeedback,
                parentFeedback,
                reviewerUserId,
                candidate.ConfidenceScore,
                candidate.Reason);
            confirmedIncidentId = linkedIncidentId == Guid.Empty ? null : linkedIncidentId;

            await _uow.SaveAsync();
            _uow.CommitTransaction();
        }
        catch
        {
            _uow.RollBack();
            throw;
        }

        const string notificationTitle = "Phản ánh đã được ghi nhận vào sự vụ hiện có";
        const string notificationMessage = "Phản ánh của bạn được xác nhận là thông tin bổ sung cho một sự vụ đã được ghi nhận. Nội dung phản ánh vẫn được lưu giữ.";
        if (confirmedIncidentId.HasValue)
        {
            await _notificationService.SendAsync(
                childFeedback.UserId,
                notificationTitle,
                notificationMessage,
                NotificationType.TicketUpdated,
                $"/community/incidents/{confirmedIncidentId.Value}",
                confirmedIncidentId,
                "Incident",
                confirmedIncidentId.Value.ToString());
        }
        else
        {
            await _notificationService.SendAsync(
                childFeedback.UserId,
                notificationTitle,
                notificationMessage,
                NotificationType.TicketUpdated,
                $"/community/feed/{parentFeedback.FeedbackId}");
        }

        return await GetCandidateDetailCoreAsync(duplicateCandidateId);
    }

    public async Task<FeedbackDuplicateCandidateDto> RejectAsync(
        Guid duplicateCandidateId,
        Guid reviewerUserId)
    {
        var feedbackId = await _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .AsNoTracking()
            .Where(candidate => candidate.DuplicateCandidateId == duplicateCandidateId)
            .Select(candidate => (Guid?)candidate.FeedbackId)
            .SingleOrDefaultAsync()
            ?? throw new Exception("Không tìm thấy đề xuất phản ánh trùng.");
        await ManagementAccessRules.EnsureManagerFeedbackReviewAccessAsync(
            _uow,
            feedbackId,
            reviewerUserId,
            requirePrimaryWhenLinked: false);
        _uow.BeginTransaction();
        try
        {
            var candidateRepository = _uow.GetRepository<FeedbackDuplicateCandidate>();
            var candidate = await candidateRepository.Entities
                .Include(c => c.Feedback)
                .FirstOrDefaultAsync(c => c.DuplicateCandidateId == duplicateCandidateId)
                ?? throw new Exception("Không tìm thấy đề xuất phản ánh trùng.");

            if (string.Equals(candidate.Status, RejectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                _uow.CommitTransaction();
                return await GetCandidateDetailCoreAsync(duplicateCandidateId);
            }

            if (!string.Equals(candidate.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Chỉ có thể từ chối đề xuất đang ở trạng thái Pending.");
            }

            var reviewedAt = DateTime.UtcNow;

            candidate.Status = RejectedStatus;
            candidate.ReviewedByUserId = reviewerUserId;
            candidate.ReviewedAt = reviewedAt;
            candidate.UpdatedAt = reviewedAt;

            var hasOtherActiveCandidate = await candidateRepository.Entities
                .AnyAsync(other =>
                    other.FeedbackId == candidate.FeedbackId
                    && other.DuplicateCandidateId != candidate.DuplicateCandidateId
                    && (other.Status == PendingStatus || other.Status == ConfirmedStatus));

            if (!candidate.Feedback.ParentTicketId.HasValue && !hasOtherActiveCandidate)
            {
                candidate.Feedback.IsMasterTicket = true;
                candidate.Feedback.UpdatedAt = reviewedAt;
            }

            await _uow.SaveAsync();
            _uow.CommitTransaction();
        }
        catch
        {
            _uow.RollBack();
            throw;
        }

        return await GetCandidateDetailCoreAsync(duplicateCandidateId);
    }

    public async Task<IReadOnlyCollection<FeedbackListItemDto>> GetLinkedFeedbacksAsync(Guid feedbackId)
    {
        await EnsureFeedbackExistsAsync(feedbackId);

        var linkedFeedbacks = await _uow.GetRepository<Feedback>().Entities
            .AsNoTrackingWithIdentityResolution()
            .Include(f => f.User)
            .Include(f => f.Area)
            .Include(f => f.Category)
            .Include(f => f.FeedbackAttachments)
            .Include(f => f.FeedbackComments)
            .Include(f => f.FeedbackSupports)
            .Include(f => f.IncidentReportLinks)
                .ThenInclude(link => link.Incident)
                    .ThenInclude(incident => incident.IncidentReportLinks)
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

    private async Task<ManagementActorScope> GetDuplicateReadActorAsync(Guid actorUserId)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(_uow, actorUserId);
        if (actor.RoleName != UserRole.SYSTEMADMIN &&
            actor.RoleName != UserRole.INTERACTIONMANAGER)
        {
            throw new UrbanService.BLL.Common.ForbiddenAccessException(
                "Staff không có quyền truy cập hàng đợi duyệt phản ánh trùng.");
        }

        return actor;
    }

    private static IQueryable<FeedbackDuplicateCandidate> ApplyCandidateReadScope(
        IQueryable<FeedbackDuplicateCandidate> candidates,
        ManagementActorScope actor)
    {
        if (actor.RoleName == UserRole.SYSTEMADMIN)
        {
            return candidates;
        }

        return candidates.Where(candidate =>
            candidate.Feedback.IncidentReportLinks.Any(link =>
                link.LinkStatus == IncidentLinkStatus.Active &&
                link.Incident.MergedIntoIncidentId == null &&
                actor.ManagerAreaIds.Contains(link.Incident.AreaId)) ||
            (!candidate.Feedback.IncidentReportLinks.Any(link =>
                link.LinkStatus == IncidentLinkStatus.Active &&
                link.Incident.MergedIntoIncidentId == null) &&
             actor.ManagerAreaIds.Contains(candidate.Feedback.AreaId)));
    }

    private async Task<FeedbackDuplicateCandidateDto> GetCandidateDetailCoreAsync(
        Guid duplicateCandidateId)
    {
        var candidate = await BaseCandidateQuery()
            .FirstOrDefaultAsync(item => item.DuplicateCandidateId == duplicateCandidateId)
            ?? throw new Exception("Không tìm thấy duplicate candidate.");
        return MapCandidate(candidate);
    }

    private IQueryable<FeedbackDuplicateCandidate> BaseCandidateQuery()
    {
        return _uow.GetRepository<FeedbackDuplicateCandidate>().Entities
            .AsNoTrackingWithIdentityResolution()
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
            .Include(c => c.Feedback)
                .ThenInclude(f => f.IncidentReportLinks)
                    .ThenInclude(link => link.Incident)
                        .ThenInclude(incident => incident.IncidentReportLinks)
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
            .Include(c => c.PotentialParentFeedback)
                .ThenInclude(f => f.IncidentReportLinks)
                    .ThenInclude(link => link.Incident)
                        .ThenInclude(incident => incident.IncidentReportLinks)
            .Include(c => c.ReviewedByUser);
    }

    private IQueryable<Feedback> FeedbackListQuery()
    {
        return _uow.GetRepository<Feedback>().Entities
            .AsNoTrackingWithIdentityResolution()
            .Include(f => f.User)
            .Include(f => f.Area)
            .Include(f => f.Category)
            .Include(f => f.FeedbackAttachments)
            .Include(f => f.FeedbackComments)
            .Include(f => f.FeedbackSupports)
            .Include(f => f.IncidentReportLinks)
                .ThenInclude(link => link.Incident)
                    .ThenInclude(incident => incident.IncidentReportLinks);
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
        var currentIncidentId = candidate.Feedback.IncidentReportLinks
            .Where(link => link.LinkStatus == IncidentLinkStatus.Active)
            .Select(link => (Guid?)link.IncidentId)
            .FirstOrDefault();
        var suggestedIncidentId = candidate.PotentialParentFeedback.IncidentReportLinks
            .Where(link => link.LinkStatus == IncidentLinkStatus.Active)
            .Select(link => (Guid?)link.IncidentId)
            .FirstOrDefault();

        return new FeedbackDuplicateCandidateDto
        {
            DuplicateCandidateId = candidate.DuplicateCandidateId,
            FeedbackId = candidate.FeedbackId,
            PotentialParentFeedbackId = candidate.PotentialParentFeedbackId,
            IncidentId = currentIncidentId,
            CurrentIncidentId = currentIncidentId,
            SuggestedIncidentId = suggestedIncidentId,
            AreInSameIncident = currentIncidentId.HasValue && currentIncidentId == suggestedIncidentId,
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
            Latitude = feedback.Latitude,
            Longitude = feedback.Longitude,
            Priority = feedback.Priority,
            Severity = feedback.Severity,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt,
            UpdatedAt = feedback.UpdatedAt,
            AttachmentCount = feedback.FeedbackAttachments.Count,
            CommentCount = feedback.FeedbackComments.Count,
            SupportCount = feedback.FeedbackSupports.Count,
            DuplicateWarning = false,
            PotentialDuplicate = null,
            ParentTicketId = feedback.ParentTicketId,
            IsMasterTicket = feedback.IsMasterTicket,
            IncidentId = feedback.IncidentReportLinks
                .Where(link => link.LinkStatus == IncidentLinkStatus.Active)
                .Select(link => (Guid?)link.IncidentId)
                .FirstOrDefault(),
            IncidentReportCount = feedback.IncidentReportLinks
                .Where(link => link.LinkStatus == IncidentLinkStatus.Active)
                .Select(link => link.Incident.IncidentReportLinks.Count(item => item.LinkStatus == IncidentLinkStatus.Active))
                .FirstOrDefault(),
            IncidentLinkStatus = feedback.IncidentReportLinks
                .Where(link => link.LinkStatus == IncidentLinkStatus.Active)
                .Select(link => link.LinkStatus)
                .FirstOrDefault()
        };
    }
}
