using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

internal sealed record ManagementActorScope(
    Guid UserId,
    string RoleName,
    IReadOnlySet<int> ManagerAreaIds);

internal sealed record IncidentAccessContext(
    Guid IncidentId,
    int AreaId,
    int? CategoryId,
    string Status,
    Guid? AssignedStaffUserId,
    Guid FeedbackId,
    string LinkRole);

internal static class ManagementAccessRules
{
    public static async Task<ManagementActorScope> GetActorScopeAsync(
        IUnitOfWork uow,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException();
        }

        var actor = await uow.GetRepository<User>().Entities
            .AsNoTracking()
            .Where(user => user.UserId == actorUserId && user.IsActive)
            .Select(user => new
            {
                user.UserId,
                user.Role.RoleName
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("Tài khoản không tồn tại hoặc đã bị khóa.");

        var normalizedRoleName = actor.RoleName.ToUpperInvariant();
        if (normalizedRoleName != UserRole.SYSTEMADMIN &&
            normalizedRoleName != UserRole.SYSTEMSTAFF &&
            normalizedRoleName != UserRole.INTERACTIONMANAGER)
        {
            throw new ForbiddenAccessException("Tài khoản không có quyền truy cập nghiệp vụ quản lý.");
        }

        IReadOnlySet<int> managerAreaIds = new HashSet<int>();
        if (normalizedRoleName == UserRole.INTERACTIONMANAGER)
        {
            managerAreaIds = (await uow.GetRepository<ManagerAreaAssignment>().Entities
                    .AsNoTracking()
                    .Where(assignment =>
                        assignment.ManagerUserId == actorUserId &&
                        assignment.IsActive &&
                        assignment.Area.IsActive)
                    .Select(assignment => assignment.AreaId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        return new ManagementActorScope(actor.UserId, normalizedRoleName, managerAreaIds);
    }

    public static IQueryable<Incident> ApplyIncidentReadScope(
        IQueryable<Incident> incidents,
        ManagementActorScope actor)
    {
        return actor.RoleName switch
        {
            UserRole.SYSTEMADMIN => incidents,
            UserRole.SYSTEMSTAFF => incidents.Where(
                incident => incident.AssignedStaffUserId == actor.UserId),
            UserRole.INTERACTIONMANAGER => incidents.Where(
                incident => actor.ManagerAreaIds.Contains(incident.AreaId)),
            _ => incidents.Where(_ => false)
        };
    }

    public static IQueryable<Feedback> ApplyFeedbackReadScope(
        IQueryable<Feedback> feedbacks,
        ManagementActorScope actor)
    {
        return actor.RoleName switch
        {
            UserRole.SYSTEMADMIN => feedbacks,
            UserRole.SYSTEMSTAFF => feedbacks.Where(feedback =>
                feedback.IncidentReportLinks.Any(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Incident.MergedIntoIncidentId == null &&
                    link.Incident.AssignedStaffUserId == actor.UserId)),
            UserRole.INTERACTIONMANAGER => feedbacks.Where(feedback =>
                feedback.IncidentReportLinks.Any(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Incident.MergedIntoIncidentId == null &&
                    actor.ManagerAreaIds.Contains(link.Incident.AreaId)) ||
                (!feedback.IncidentReportLinks.Any(link =>
                    link.LinkStatus == IncidentLinkStatus.Active &&
                    link.Incident.MergedIntoIncidentId == null) &&
                 actor.ManagerAreaIds.Contains(feedback.AreaId))),
            _ => feedbacks.Where(_ => false)
        };
    }

    public static void EnsureManagerRole(ManagementActorScope actor)
    {
        if (actor.RoleName != UserRole.INTERACTIONMANAGER)
        {
            throw new ForbiddenAccessException("Chỉ Manager được thực hiện thao tác này.");
        }
    }

    public static void EnsureStaffRole(ManagementActorScope actor)
    {
        if (actor.RoleName != UserRole.SYSTEMSTAFF)
        {
            throw new ForbiddenAccessException("Chỉ Staff được thực hiện thao tác này.");
        }
    }

    public static void EnsureManagerArea(ManagementActorScope actor, int areaId)
    {
        EnsureManagerRole(actor);
        if (!actor.ManagerAreaIds.Contains(areaId))
        {
            throw new ForbiddenAccessException("Manager không phụ trách phường của sự vụ này.");
        }
    }

    public static async Task<IncidentAccessContext> EnsureFeedbackReadAccessAsync(
        IUnitOfWork uow,
        Guid feedbackId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(uow, actorUserId, cancellationToken);
        var feedbacks = ApplyFeedbackReadScope(
            uow.GetRepository<Feedback>().Entities.AsNoTracking(),
            actor);

        var exists = await feedbacks.AnyAsync(
            feedback => feedback.FeedbackId == feedbackId,
            cancellationToken);
        if (!exists)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem phản ánh này.");
        }

        var activeContext = await GetActiveIncidentContextAsync(uow, feedbackId, cancellationToken);
        if (activeContext is not null)
        {
            return activeContext;
        }

        if (actor.RoleName == UserRole.SYSTEMADMIN || actor.RoleName == UserRole.INTERACTIONMANAGER)
        {
            var unlinkedContext = await uow.GetRepository<Feedback>().Entities
                .AsNoTracking()
                .Where(feedback => feedback.FeedbackId == feedbackId)
                .Select(feedback => new IncidentAccessContext(
                    Guid.Empty,
                    feedback.AreaId,
                    feedback.CategoryId,
                    feedback.Status,
                    null,
                    feedback.FeedbackId,
                    string.Empty))
                .SingleAsync(cancellationToken);
            if (actor.RoleName == UserRole.INTERACTIONMANAGER)
            {
                EnsureManagerArea(actor, unlinkedContext.AreaId);
            }

            return unlinkedContext;
        }

        throw new ForbiddenAccessException("Phản ánh không thuộc sự vụ đang hoạt động.");
    }

    public static async Task<IncidentAccessContext> EnsureStaffFeedbackOperationAsync(
        IUnitOfWork uow,
        Guid feedbackId,
        Guid staffUserId,
        bool requirePrimary = true,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(uow, staffUserId, cancellationToken);
        EnsureStaffRole(actor);
        var context = await GetActiveIncidentContextAsync(uow, feedbackId, cancellationToken)
            ?? throw new ForbiddenAccessException("Phản ánh không thuộc sự vụ đang hoạt động.");

        if (context.AssignedStaffUserId != staffUserId)
        {
            throw new ForbiddenAccessException("Sự vụ chưa được phân công cho Staff hiện tại.");
        }

        if (requirePrimary && context.LinkRole != IncidentLinkRole.Primary)
        {
            throw new ForbiddenAccessException("Chỉ phản ánh chính của sự vụ được dùng để làm việc với bên thứ ba.");
        }

        return context;
    }

    public static async Task<IncidentAccessContext> EnsureManagerFeedbackOperationAsync(
        IUnitOfWork uow,
        Guid feedbackId,
        Guid managerUserId,
        bool requirePrimary = true,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(uow, managerUserId, cancellationToken);
        var context = await GetActiveIncidentContextAsync(uow, feedbackId, cancellationToken)
            ?? throw new ForbiddenAccessException("Phản ánh không thuộc sự vụ đang hoạt động.");
        EnsureManagerArea(actor, context.AreaId);

        if (requirePrimary && context.LinkRole != IncidentLinkRole.Primary)
        {
            throw new ForbiddenAccessException("Chỉ phản ánh chính của sự vụ được xử lý độc lập.");
        }

        return context;
    }

    public static async Task<IncidentAccessContext> EnsureManagerFeedbackReviewAccessAsync(
        IUnitOfWork uow,
        Guid feedbackId,
        Guid managerUserId,
        bool requirePrimaryWhenLinked = true,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(uow, managerUserId, cancellationToken);
        EnsureManagerRole(actor);
        var context = await EnsureFeedbackReadAccessAsync(
            uow,
            feedbackId,
            managerUserId,
            cancellationToken);
        EnsureManagerArea(actor, context.AreaId);

        if (requirePrimaryWhenLinked &&
            context.IncidentId != Guid.Empty &&
            context.LinkRole != IncidentLinkRole.Primary)
        {
            throw new ForbiddenAccessException("Chỉ phản ánh chính của sự vụ được xử lý độc lập.");
        }

        return context;
    }

    public static async Task<IncidentAccessContext> EnsureStaffIncidentOperationAsync(
        IUnitOfWork uow,
        Guid incidentId,
        Guid staffUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(uow, staffUserId, cancellationToken);
        EnsureStaffRole(actor);

        var context = await uow.GetRepository<Incident>().Entities
            .AsNoTracking()
            .Where(incident =>
                incident.IncidentId == incidentId &&
                incident.MergedIntoIncidentId == null)
            .Select(incident => new IncidentAccessContext(
                incident.IncidentId,
                incident.AreaId,
                incident.CategoryId,
                incident.Status,
                incident.AssignedStaffUserId,
                Guid.Empty,
                string.Empty))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("Incident does not exist or is no longer active.");

        if (context.AssignedStaffUserId != staffUserId)
        {
            throw new ForbiddenAccessException("Incident is not assigned to the current staff user.");
        }

        return context;
    }

    public static async Task EnsureIncidentReadAccessAsync(
        IUnitOfWork uow,
        Guid incidentId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorScopeAsync(uow, actorUserId, cancellationToken);
        var canRead = await ApplyIncidentReadScope(
                uow.GetRepository<Incident>().Entities.AsNoTracking(),
                actor)
            .AnyAsync(incident => incident.IncidentId == incidentId, cancellationToken);

        if (!canRead)
        {
            throw new ForbiddenAccessException("You do not have access to this incident.");
        }
    }

    public static async Task<Guid> GetProviderAssignmentIncidentIdAsync(
        IUnitOfWork uow,
        int providerAssignmentId,
        CancellationToken cancellationToken = default)
    {
        return await uow.GetRepository<FeedbackProviderReport>().Entities
            .AsNoTracking()
            .Where(report => report.ProviderReportId == providerAssignmentId)
            .Select(report => (Guid?)report.IncidentId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Provider report không tồn tại.");
    }

    private static async Task<IncidentAccessContext?> GetActiveIncidentContextAsync(
        IUnitOfWork uow,
        Guid feedbackId,
        CancellationToken cancellationToken)
    {
        return await uow.GetRepository<IncidentReportLink>().Entities
            .AsNoTracking()
            .Where(link =>
                link.FeedbackId == feedbackId &&
                link.LinkStatus == IncidentLinkStatus.Active &&
                link.Incident.MergedIntoIncidentId == null)
            .Select(link => new IncidentAccessContext(
                link.IncidentId,
                link.Incident.AreaId,
                link.Incident.CategoryId,
                link.Incident.Status,
                link.Incident.AssignedStaffUserId,
                link.FeedbackId,
                link.LinkRole))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
