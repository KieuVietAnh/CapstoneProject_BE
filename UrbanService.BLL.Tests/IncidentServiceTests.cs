using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public sealed class IncidentServiceTests
{
    [Fact]
    public async Task VerifyReport_CopiesManagerClassificationIntoNewIncident()
    {
        var context = new IncidentTestContext();
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        feedback.Status = FeedbackStatus.AiReviewed;
        feedback.CategoryId = 10;
        feedback.Priority = "High";
        feedback.Severity = IncidentSeverity.Critical;
        context.Feedbacks.Add(feedback);

        await new IncidentService(context.UnitOfWork).VerifyReportAsync(feedback.FeedbackId, Guid.NewGuid());

        var incident = Assert.Single(context.Incidents);
        Assert.Equal(10, incident.CategoryId);
        Assert.Equal("High", incident.Priority);
        Assert.Equal(IncidentSeverity.Critical, incident.Severity);
        Assert.Equal(IncidentLinkRole.Primary, Assert.Single(context.Links).LinkRole);
    }

    [Fact]
    public async Task VerifyReport_CreatesVerifiedIncidentLinkSubscriptionAndEvents()
    {
        var context = new IncidentTestContext();
        var notificationService = Substitute.For<INotificationService>();
        var service = new IncidentService(context.UnitOfWork, notificationService);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.AiReviewed;
        context.Feedbacks.Add(feedback);
        var managerUserId = Guid.NewGuid();

        var history = await service.VerifyReportAsync(
            feedback.FeedbackId,
            managerUserId,
            "Manager confirmed report");

        var incident = Assert.Single(context.Incidents);
        Assert.Equal(feedback.AreaId, incident.AreaId);
        Assert.Equal(IncidentStatus.Verified, incident.Status);
        Assert.Equal(IncidentSeverity.Medium, incident.Severity);
        Assert.Equal(FeedbackStatus.Verified, feedback.Status);
        Assert.Equal(FeedbackStatus.AiReviewed, history.OldStatus);
        Assert.Equal(FeedbackStatus.Verified, history.NewStatus);

        var link = Assert.Single(context.Links);
        Assert.Equal(incident.IncidentId, link.IncidentId);
        Assert.Equal(feedback.FeedbackId, link.FeedbackId);
        Assert.Equal(IncidentLinkStatus.Active, link.LinkStatus);
        Assert.Equal(IncidentLinkMethod.Created, link.LinkMethod);
        Assert.Equal(IncidentLinkRole.Primary, link.LinkRole);

        var subscription = Assert.Single(context.Subscriptions);
        Assert.Equal(incident.IncidentId, subscription.IncidentId);
        Assert.Equal(feedback.UserId, subscription.UserId);
        Assert.True(subscription.IsActive);

        Assert.Equal(3, context.Events.Count);
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.IncidentCreated);
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.ReportLinked);
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.StatusChanged);
        await notificationService.Received(1).SendAsync(
            feedback.UserId,
            "Sự vụ đã được xác nhận",
            Arg.Is<string>(message => message.Contains(feedback.Title, StringComparison.Ordinal)),
            NotificationType.TicketUpdated,
            $"/community/incidents/{incident.IncidentId}",
            incident.IncidentId,
            "Incident",
            incident.IncidentId.ToString());
        context.UnitOfWork.Received(1).CommitTransaction();
    }

    [Fact]
    public async Task UpdateStatus_NotifiesEachActiveSubscriberOnceWithVietnameseContent()
    {
        var context = new IncidentTestContext();
        var notificationService = Substitute.For<INotificationService>();
        var service = new IncidentService(context.UnitOfWork, notificationService);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));
        var activeUserId = Guid.NewGuid();
        var activeUser = new User
        {
            UserId = activeUserId,
            FullName = "Active subscriber",
            Email = "active-subscriber@example.test"
        };
        var inactiveUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Inactive subscriber",
            Email = "inactive-subscriber@example.test"
        };
        context.Subscriptions.AddRange(
        [
            new IncidentSubscription
            {
                IncidentSubscriptionId = Guid.NewGuid(),
                IncidentId = incident.IncidentId,
                UserId = activeUserId,
                User = activeUser,
                SourceType = IncidentSubscriptionSource.Manual,
                IsActive = true,
                CreatedAt = now
            },
            new IncidentSubscription
            {
                IncidentSubscriptionId = Guid.NewGuid(),
                IncidentId = incident.IncidentId,
                UserId = activeUserId,
                User = activeUser,
                SourceType = IncidentSubscriptionSource.Report,
                IsActive = true,
                CreatedAt = now
            },
            new IncidentSubscription
            {
                IncidentSubscriptionId = Guid.NewGuid(),
                IncidentId = incident.IncidentId,
                UserId = inactiveUser.UserId,
                User = inactiveUser,
                SourceType = IncidentSubscriptionSource.Manual,
                IsActive = false,
                CreatedAt = now
            }
        ]);

        await service.UpdateStatusFromFeedbackAsync(
            feedback.FeedbackId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.InProgress
            },
            Guid.NewGuid());

        await notificationService.Received(1).SendAsync(
            activeUserId,
            "Sự vụ đang được xử lý",
            Arg.Is<string>(message =>
                message.Contains(feedback.Title, StringComparison.Ordinal) &&
                !message.Contains(IncidentStatus.InProgress, StringComparison.Ordinal)),
            NotificationType.TicketUpdated,
            $"/community/incidents/{incident.IncidentId}",
            incident.IncidentId,
            "Incident",
            incident.IncidentId.ToString());
        Assert.Single(notificationService.ReceivedCalls());
    }

    [Fact]
    public async Task NotifyContentUpdated_UsesIncidentIdentityForActiveSubscribers()
    {
        var context = new IncidentTestContext();
        var notificationService = Substitute.For<INotificationService>();
        var service = new IncidentService(context.UnitOfWork, notificationService);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        var subscriberId = Guid.NewGuid();
        context.Incidents.Add(incident);
        context.Subscriptions.Add(new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = subscriberId,
            SourceType = IncidentSubscriptionSource.Manual,
            IsActive = true,
            CreatedAt = now
        });

        await service.NotifyContentUpdatedAsync(incident.IncidentId);

        await notificationService.Received(1).SendAsync(
            subscriberId,
            "Sự vụ có thông tin mới",
            Arg.Is<string>(message => message.Contains(incident.Title, StringComparison.Ordinal)),
            NotificationType.TicketUpdated,
            $"/community/incidents/{incident.IncidentId}",
            incident.IncidentId,
            "Incident",
            incident.IncidentId.ToString());
    }

    [Fact]
    public async Task RelinkConfirmedDuplicate_WhenChildHasNoIncident_LinksCanonicalWithoutCreatingIncident()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var createdAt = DateTime.UtcNow;
        var parent = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), createdAt.AddMinutes(-10));
        var child = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), createdAt);
        parent.Status = FeedbackStatus.Verified;
        child.Status = FeedbackStatus.AiReviewed;
        var canonicalIncident = IncidentTestContext.Incident(
            Guid.NewGuid(),
            parent,
            createdAt.AddMinutes(-5));
        canonicalIncident.Status = IncidentStatus.Verified;
        context.Incidents.Add(canonicalIncident);
        context.Links.Add(IncidentTestContext.Link(
            canonicalIncident,
            parent,
            IncidentLinkRole.Primary,
            createdAt.AddMinutes(-5)));

        var result = await service.RelinkConfirmedDuplicateAsync(
            child,
            parent,
            Guid.NewGuid(),
            0.94m,
            "Cùng sự vụ và vị trí");

        Assert.Equal(canonicalIncident.IncidentId, result);
        Assert.Single(context.Incidents);
        var childLink = Assert.Single(context.Links.Where(link => link.FeedbackId == child.FeedbackId));
        Assert.Equal(canonicalIncident.IncidentId, childLink.IncidentId);
        Assert.Equal(IncidentLinkRole.Corroborating, childLink.LinkRole);
        Assert.DoesNotContain(context.Events, item => item.EventType == IncidentEventType.IncidentMerged);
        Assert.Equal(FeedbackStatus.Verified, child.Status);
    }

    [Fact]
    public async Task RelinkConfirmedDuplicate_MovesChildAndMergesEmptyIncident()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var createdAt = DateTime.UtcNow;
        var parent = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), createdAt.AddMinutes(-10));
        var child = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), createdAt);
        parent.Status = FeedbackStatus.Verified;
        child.Status = FeedbackStatus.AiReviewed;
        var targetIncident = IncidentTestContext.Incident(Guid.NewGuid(), parent, createdAt.AddMinutes(-10));
        var childIncident = IncidentTestContext.Incident(Guid.NewGuid(), child, createdAt);
        childIncident.Status = IncidentStatus.New;
        context.Incidents.AddRange([targetIncident, childIncident]);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(targetIncident, parent, IncidentLinkRole.Primary, createdAt.AddMinutes(-10)),
            IncidentTestContext.Link(childIncident, child, IncidentLinkRole.Primary, createdAt)
        ]);
        context.Subscriptions.Add(new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = childIncident.IncidentId,
            UserId = child.UserId,
            SourceType = IncidentSubscriptionSource.Report,
            SourceFeedbackId = child.FeedbackId,
            IsActive = true,
            CreatedAt = createdAt
        });

        var result = await service.RelinkConfirmedDuplicateAsync(
            child,
            parent,
            Guid.NewGuid(),
            0.94m,
            "Cùng sự vụ và vị trí");

        Assert.Equal(targetIncident.IncidentId, result);
        var oldChildLink = context.Links.Single(link =>
            link.FeedbackId == child.FeedbackId && link.IncidentId == childIncident.IncidentId);
        Assert.Equal(IncidentLinkStatus.Unlinked, oldChildLink.LinkStatus);
        Assert.NotNull(oldChildLink.UnlinkedAt);

        var activeChildLink = context.Links.Single(link =>
            link.FeedbackId == child.FeedbackId && link.LinkStatus == IncidentLinkStatus.Active);
        Assert.Equal(targetIncident.IncidentId, activeChildLink.IncidentId);
        Assert.Equal(IncidentLinkMethod.StaffConfirmed, activeChildLink.LinkMethod);
        Assert.Equal(IncidentLinkRole.Corroborating, activeChildLink.LinkRole);
        Assert.Equal(0.94m, activeChildLink.ConfidenceScore);

        Assert.Equal("Merged", childIncident.Status);
        Assert.Equal(targetIncident.IncidentId, childIncident.MergedIntoIncidentId);
        Assert.False(context.Subscriptions.Single(item =>
            item.IncidentId == childIncident.IncidentId && item.UserId == child.UserId).IsActive);
        Assert.Contains(context.Subscriptions, item =>
            item.IncidentId == targetIncident.IncidentId &&
            item.UserId == child.UserId &&
            item.IsActive);
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.IncidentMerged);
        Assert.Equal(FeedbackStatus.Verified, child.Status);
        Assert.Contains(context.StatusHistories, history =>
            history.FeedbackId == child.FeedbackId &&
            history.OldStatus == FeedbackStatus.AiReviewed &&
            history.NewStatus == FeedbackStatus.Verified);
    }

    [Fact]
    public async Task RelinkConfirmedDuplicate_WhenAlreadyLinked_RepairsStatusProjection()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var parent = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var child = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        child.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), parent, now.AddMinutes(-5));
        incident.Status = IncidentStatus.InProgress;
        context.Incidents.Add(incident);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(incident, parent, IncidentLinkRole.Primary, now.AddMinutes(-5)),
            IncidentTestContext.Link(incident, child, IncidentLinkRole.Corroborating, now)
        ]);

        var result = await service.RelinkConfirmedDuplicateAsync(
            child,
            parent,
            Guid.NewGuid(),
            0.94m,
            "Retry duplicate confirmation");

        Assert.Equal(incident.IncidentId, result);
        Assert.Equal(2, context.Links.Count);
        Assert.Equal(FeedbackStatus.InProgress, child.Status);
        Assert.Contains(context.StatusHistories, history =>
            history.FeedbackId == child.FeedbackId &&
            history.OldStatus == FeedbackStatus.AiReviewed &&
            history.NewStatus == FeedbackStatus.InProgress);
    }

    [Fact]
    public async Task RelinkConfirmedDuplicate_RejectsMergeAfterProviderProcessingStarted()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var parent = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var child = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        parent.Status = FeedbackStatus.InProgress;
        child.Status = FeedbackStatus.AiReviewed;
        var targetIncident = IncidentTestContext.Incident(Guid.NewGuid(), parent, now.AddMinutes(-5));
        var childIncident = IncidentTestContext.Incident(Guid.NewGuid(), child, now);
        context.Incidents.AddRange([targetIncident, childIncident]);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(targetIncident, parent, IncidentLinkRole.Primary, now.AddMinutes(-5)),
            IncidentTestContext.Link(childIncident, child, IncidentLinkRole.Primary, now)
        ]);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.RelinkConfirmedDuplicateAsync(
                child,
                parent,
                Guid.NewGuid(),
                0.94m,
                "Late duplicate confirmation"));

        Assert.Contains("before staff assignment", exception.Message);
        Assert.Equal(childIncident.IncidentId, context.Links.Single(link =>
            link.FeedbackId == child.FeedbackId && link.LinkStatus == IncidentLinkStatus.Active).IncidentId);
    }

    [Fact]
    public async Task UpdateStatusFromFeedback_ProjectsStatusToAllActiveReports()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var primary = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var corroborating = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        primary.Status = FeedbackStatus.Verified;
        corroborating.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), primary, now.AddMinutes(-5));
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.AddRange([primary, corroborating]);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(incident, primary, IncidentLinkRole.Primary, now.AddMinutes(-5)),
            IncidentTestContext.Link(incident, corroborating, IncidentLinkRole.Corroborating, now)
        ]);
        var actorUserId = Guid.NewGuid();

        var result = await service.UpdateStatusFromFeedbackAsync(
            primary.FeedbackId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.Assigned,
                Note = "Assigned to provider"
            },
            actorUserId);

        Assert.Equal(primary.FeedbackId, result.FeedbackId);
        Assert.Equal(FeedbackStatus.Assigned, result.NewStatus);
        Assert.Equal(IncidentStatus.Assigned, incident.Status);
        Assert.Equal(FeedbackStatus.Assigned, primary.Status);
        Assert.Equal(FeedbackStatus.Assigned, corroborating.Status);
        Assert.Equal(2, context.StatusHistories.Count);
        Assert.All(context.StatusHistories, history =>
        {
            Assert.Equal(FeedbackStatus.Assigned, history.NewStatus);
            Assert.Equal(actorUserId, history.ChangedByUserId);
            Assert.Equal("Assigned to provider", history.Note);
        });
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.StatusChanged);
    }

    [Fact]
    public async Task UpdateStatusFromFeedback_DoesNotProjectStatusToUnlinkedReport()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var active = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var unlinked = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        active.Status = FeedbackStatus.Verified;
        unlinked.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), active, now.AddMinutes(-5));
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.AddRange([active, unlinked]);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            active,
            IncidentLinkRole.Primary,
            now.AddMinutes(-5)));
        var oldLink = IncidentTestContext.Link(
            incident,
            unlinked,
            IncidentLinkRole.Corroborating,
            now);
        oldLink.LinkStatus = IncidentLinkStatus.Unlinked;
        oldLink.UnlinkedAt = now;
        context.Links.Add(oldLink);

        await service.UpdateStatusFromFeedbackAsync(
            active.FeedbackId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.InProgress
            },
            Guid.NewGuid());

        Assert.Equal(FeedbackStatus.InProgress, active.Status);
        Assert.Equal(FeedbackStatus.AiReviewed, unlinked.Status);
        Assert.Single(context.StatusHistories);
        Assert.Equal(active.FeedbackId, context.StatusHistories[0].FeedbackId);
    }

    [Fact]
    public async Task UpdateStatusFromFeedback_ResolvesActiveIncidentAndReturnsLegacyHistory()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));
        var actorUserId = Guid.NewGuid();

        var result = await service.UpdateStatusFromFeedbackAsync(
            feedback.FeedbackId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.InProgress,
                Note = "Legacy route"
            },
            actorUserId);

        Assert.Equal(feedback.FeedbackId, result.FeedbackId);
        Assert.Equal(FeedbackStatus.Verified, result.OldStatus);
        Assert.Equal(FeedbackStatus.InProgress, result.NewStatus);
        Assert.Equal("Legacy route", result.Note);
        Assert.Equal(IncidentStatus.InProgress, incident.Status);
    }

    [Fact]
    public async Task UpdateStatusFromFeedback_RejectsRollbackToNew()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusFromFeedbackAsync(
            feedback.FeedbackId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.New
            },
            Guid.NewGuid()));

        Assert.Equal(IncidentStatus.Verified, incident.Status);
        Assert.Equal(FeedbackStatus.Verified, feedback.Status);
        Assert.Empty(context.StatusHistories);
        context.UnitOfWork.Received(1).RollBack();
    }

    [Fact]
    public async Task UpdateStatus_DirectVerify_IsRejectedToPreventFeedbackWorkflowBypass()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.New;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));
        var manager = context.AddActor(UserRole.INTERACTIONMANAGER, "Ward manager");
        context.AddManagerAreaAssignment(manager, incident.Area);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.Verified,
                Note = "Manager verified report"
            },
            manager.UserId));

        Assert.Equal(IncidentStatus.New, incident.Status);
        Assert.Equal(FeedbackStatus.AiReviewed, feedback.Status);
        Assert.Empty(context.StatusHistories);
    }

    [Theory]
    [InlineData(IncidentStatus.Approved, FeedbackStatus.Approved)]
    [InlineData(IncidentStatus.NeedRework, FeedbackStatus.NeedRework)]
    public async Task UpdateStatusFromResolutionReview_AllowsReviewTransitions(
        string incidentStatus,
        string feedbackStatus)
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.SubmittedForApproval;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.SubmittedForApproval;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));
        var manager = context.AddActor(UserRole.INTERACTIONMANAGER, "Ward manager");
        context.AddManagerAreaAssignment(manager, incident.Area);

        var result = await service.UpdateStatusFromResolutionReviewAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = incidentStatus,
                Note = "Resolution reviewed"
            },
            manager.UserId);

        Assert.Equal(incidentStatus, result.Status);
        Assert.Equal(incidentStatus, incident.Status);
        Assert.Equal(feedbackStatus, feedback.Status);
        var history = Assert.Single(context.StatusHistories);
        Assert.Equal(FeedbackStatus.SubmittedForApproval, history.OldStatus);
        Assert.Equal(feedbackStatus, history.NewStatus);
    }

    [Fact]
    public async Task UpdateStatusFromResolutionReview_RejectsNonReviewTransition()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.SubmittedForApproval;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.SubmittedForApproval;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));
        var manager = context.AddActor(UserRole.INTERACTIONMANAGER, "Ward manager");
        context.AddManagerAreaAssignment(manager, incident.Area);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusFromResolutionReviewAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.InProgress
            },
            manager.UserId));

        Assert.Equal(IncidentStatus.SubmittedForApproval, incident.Status);
        Assert.Equal(FeedbackStatus.SubmittedForApproval, feedback.Status);
        Assert.Empty(context.StatusHistories);
    }

    [Fact]
    public async Task GetIncidents_StaffSeesOnlyIncidentsAssignedToSelf()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var staff = context.AddActor(UserRole.SYSTEMSTAFF, "Assigned staff");
        var assignedFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var assignedIncident = IncidentTestContext.Incident(Guid.NewGuid(), assignedFeedback, now);
        assignedIncident.AssignedStaffUserId = staff.UserId;
        var otherFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(1));
        var otherIncident = IncidentTestContext.Incident(Guid.NewGuid(), otherFeedback, now.AddMinutes(1));
        otherIncident.AssignedStaffUserId = Guid.NewGuid();
        context.Incidents.AddRange([assignedIncident, otherIncident]);

        var result = await service.GetIncidentsAsync(
            new UrbanService.BLL.Dtos.IncidentQueryParameters(),
            staff.UserId);

        var item = Assert.Single(result.Items);
        Assert.Equal(assignedIncident.IncidentId, item.IncidentId);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task GetIncidentDetail_StaffCanReadAssignedIncidentAndIsDeniedUnassignedIncident()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var staff = context.AddActor(UserRole.SYSTEMSTAFF, "Assigned staff");
        var assignedFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var assignedIncident = IncidentTestContext.Incident(Guid.NewGuid(), assignedFeedback, now);
        assignedIncident.AssignedStaffUserId = staff.UserId;
        var otherFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(1));
        var otherIncident = IncidentTestContext.Incident(Guid.NewGuid(), otherFeedback, now.AddMinutes(1));
        otherIncident.AssignedStaffUserId = Guid.NewGuid();
        context.Incidents.AddRange([assignedIncident, otherIncident]);

        var detail = await service.GetIncidentDetailAsync(
            assignedIncident.IncidentId,
            staff.UserId);

        Assert.Equal(assignedIncident.IncidentId, detail.IncidentId);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.GetIncidentDetailAsync(
            otherIncident.IncidentId,
            staff.UserId));
    }

    [Fact]
    public async Task GetIncidents_ManagerSeesOnlyIncidentsInCoveredAreas()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var manager = context.AddActor(UserRole.INTERACTIONMANAGER, "Ward manager");
        var coveredFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var coveredIncident = IncidentTestContext.Incident(Guid.NewGuid(), coveredFeedback, now);
        var outsideFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(1));
        outsideFeedback.AreaId = 2;
        var outsideIncident = IncidentTestContext.Incident(Guid.NewGuid(), outsideFeedback, now.AddMinutes(1));
        context.Incidents.AddRange([coveredIncident, outsideIncident]);
        context.AddManagerAreaAssignment(manager, coveredIncident.Area);

        var result = await service.GetIncidentsAsync(
            new UrbanService.BLL.Dtos.IncidentQueryParameters(),
            manager.UserId);

        var item = Assert.Single(result.Items);
        Assert.Equal(coveredIncident.IncidentId, item.IncidentId);
        Assert.Equal(coveredIncident.AreaId, item.AreaId);
    }

    [Fact]
    public async Task GetAssigneeCandidates_MixedCaseRoles_ReturnsOnlyActiveStaffForIncidentScope()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        context.Incidents.Add(incident);
        var manager = context.AddActor("InteractionManager", "Ward manager");
        context.AddManagerAreaAssignment(manager, incident.Area);

        var eligibleStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Eligible staff");
        eligibleStaff.Role.RoleName = "SystemStaff";
        var wrongAreaStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Wrong area staff");
        var wrongCategoryStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Wrong category staff");
        var inactiveStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Inactive staff");
        inactiveStaff.IsActive = false;
        var otherArea = new OperatingArea
        {
            AreaId = incident.AreaId + 1,
            AreaName = "Area 2",
            AreaType = "Ward",
            IsActive = true
        };
        context.Assignments.AddRange(
        [
            IncidentTestContext.Assignment(eligibleStaff, incident.Area, incident.CategoryId),
            IncidentTestContext.Assignment(wrongAreaStaff, otherArea, incident.CategoryId),
            IncidentTestContext.Assignment(wrongCategoryStaff, incident.Area, incident.CategoryId + 1),
            IncidentTestContext.Assignment(inactiveStaff, incident.Area, incident.CategoryId)
        ]);

        var result = await service.GetAssigneeCandidatesAsync(
            incident.IncidentId,
            manager.UserId);

        var candidate = Assert.Single(result);
        Assert.Equal(eligibleStaff.UserId, candidate.UserId);
        Assert.Equal(incident.AreaId, candidate.AreaId);
        Assert.Equal(incident.CategoryId, candidate.CategoryId);
    }

    [Fact]
    public async Task Assign_MixedCaseRoles_ManagerSetsStaffAndProjectsAssignedStatusToAllActiveReports()
    {
        var context = new IncidentTestContext();
        var notificationService = Substitute.For<INotificationService>();
        var service = new IncidentService(context.UnitOfWork, notificationService);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var corroborating = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(1));
        feedback.Status = FeedbackStatus.Verified;
        corroborating.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.AddRange([feedback, corroborating]);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(incident, feedback, IncidentLinkRole.Primary, now),
            IncidentTestContext.Link(incident, corroborating, IncidentLinkRole.Corroborating, now.AddMinutes(1))
        ]);
        var staff = IncidentTestContext.Staff(Guid.NewGuid(), "Assigned staff");
        staff.Role.RoleName = "SystemStaff";
        context.Assignments.Add(IncidentTestContext.Assignment(staff, incident.Area, incident.CategoryId));
        var manager = context.AddActor("InteractionManager", "Ward manager");
        context.AddManagerAreaAssignment(manager, incident.Area);
        context.Subscriptions.Add(new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = feedback.UserId,
            User = feedback.User,
            SourceType = IncidentSubscriptionSource.Report,
            SourceFeedbackId = feedback.FeedbackId,
            IsActive = true,
            CreatedAt = now
        });

        var result = await service.AssignAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.AssignIncidentRequest
            {
                StaffUserId = staff.UserId,
                Reason = "Phụ trách đúng phường và danh mục"
            },
            manager.UserId);

        Assert.Equal(staff.UserId, incident.AssignedStaffUserId);
        Assert.Equal(staff.UserId, result.AssignedStaffUserId);
        Assert.Equal(IncidentStatus.Assigned, incident.Status);
        Assert.Equal(FeedbackStatus.Assigned, feedback.Status);
        Assert.Equal(FeedbackStatus.Assigned, corroborating.Status);
        Assert.Equal(2, context.StatusHistories.Count(history =>
            history.NewStatus == FeedbackStatus.Assigned &&
            history.ChangedByUserId == manager.UserId));
        Assert.Contains(context.Events, incidentEvent =>
            incidentEvent.IncidentId == incident.IncidentId &&
            incidentEvent.EventType == IncidentEventType.AssigneeChanged &&
            incidentEvent.ActorUserId == manager.UserId &&
            incidentEvent.PayloadJson != null &&
            incidentEvent.PayloadJson.Contains(staff.UserId.ToString(), StringComparison.Ordinal));
        Assert.Contains(context.Events, incidentEvent =>
            incidentEvent.IncidentId == incident.IncidentId &&
            incidentEvent.EventType == IncidentEventType.StatusChanged &&
            incidentEvent.ActorUserId == manager.UserId);
        await notificationService.Received(1).SendAsync(
            feedback.UserId,
            "Sự vụ đã được phân công",
            Arg.Is<string>(message => message.Contains(feedback.Title, StringComparison.Ordinal)),
            NotificationType.TicketUpdated,
            $"/community/incidents/{incident.IncidentId}",
            incident.IncidentId,
            "Incident",
            incident.IncidentId.ToString());
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task Assign_RejectsStaffOutsideIncidentCategory()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        var staff = IncidentTestContext.Staff(Guid.NewGuid(), "Wrong category staff");
        context.Assignments.Add(IncidentTestContext.Assignment(staff, incident.Area, incident.CategoryId + 1));
        var manager = context.AddActor(UserRole.INTERACTIONMANAGER, "Ward manager");
        context.AddManagerAreaAssignment(manager, incident.Area);

        var exception = await Assert.ThrowsAsync<Exception>(() => service.AssignAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.AssignIncidentRequest { StaffUserId = staff.UserId },
            manager.UserId));

        Assert.Contains("khu vực và danh mục", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(incident.AssignedStaffUserId);
        Assert.DoesNotContain(context.Events, incidentEvent =>
            incidentEvent.EventType == IncidentEventType.AssigneeChanged);
    }

    [Fact]
    public async Task UnlinkReport_PreservesHistoryAndPromotesRemainingPrimary()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var primaryReport = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var remainingReport = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), primaryReport, now.AddMinutes(-5));
        context.Incidents.Add(incident);
        var manager = context.AddActor(UserRole.INTERACTIONMANAGER, "Ward manager");
        context.AddManagerAreaAssignment(manager, incident.Area);
        var primaryLink = IncidentTestContext.Link(incident, primaryReport, IncidentLinkRole.Primary, now.AddMinutes(-5));
        var remainingLink = IncidentTestContext.Link(incident, remainingReport, IncidentLinkRole.Corroborating, now);
        context.Links.AddRange([primaryLink, remainingLink]);
        context.Subscriptions.Add(new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = primaryReport.UserId,
            SourceType = IncidentSubscriptionSource.Report,
            SourceFeedbackId = primaryReport.FeedbackId,
            IsActive = true,
            CreatedAt = now.AddMinutes(-5)
        });

        await service.UnlinkReportAsync(
            incident.IncidentId,
            primaryReport.FeedbackId,
            manager.UserId);

        Assert.Equal(IncidentLinkStatus.Unlinked, primaryLink.LinkStatus);
        Assert.NotNull(primaryLink.UnlinkedAt);
        Assert.Equal(IncidentLinkRole.Primary, remainingLink.LinkRole);
        Assert.False(Assert.Single(context.Subscriptions).IsActive);
        Assert.Contains(context.Events, item =>
            item.EventType == IncidentEventType.ReportUnlinked &&
            item.FeedbackId == primaryReport.FeedbackId);
        context.UnitOfWork.Received(1).CommitTransaction();
    }

    [Fact]
    public void ManagementIncidentMutations_RequireInteractionManagerRole()
    {
        var managerOnlyActions = new[]
        {
            nameof(ManagementIncidentsController.LinkReport),
            nameof(ManagementIncidentsController.UnlinkReport),
            nameof(ManagementIncidentsController.UpdateIncident),
            nameof(ManagementIncidentsController.UpdateStatus),
            nameof(ManagementIncidentsController.GetAssigneeCandidates),
            nameof(ManagementIncidentsController.Assign),
            nameof(ManagementIncidentsController.Merge)
        };

        foreach (var actionName in managerOnlyActions)
        {
            var action = typeof(ManagementIncidentsController).GetMethod(actionName)!;
            var authorize = Assert.Single(action
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Cast<AuthorizeAttribute>());
            Assert.Equal(UserRole.INTERACTIONMANAGER, authorize.Roles);
        }
    }

    [Fact]
    public async Task PublicIncidentDetail_ProjectsFeedbackImagesAndIncidentInteractionState()
    {
        var context = new IncidentTestContext();
        var now = DateTime.UtcNow;
        var currentUserId = Guid.NewGuid();
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.Verified;
        feedback.FeedbackAttachments.Add(new FeedbackAttachment
        {
            AttachmentId = 1,
            FeedbackId = feedback.FeedbackId,
            FileUrl = "https://example.test/evidence.jpg",
            FileType = "image/jpeg",
            UploadedAt = now
        });
        feedback.FeedbackAttachments.Add(new FeedbackAttachment
        {
            AttachmentId = 2,
            FeedbackId = feedback.FeedbackId,
            FileUrl = "https://example.test/evidence.pdf",
            FileType = "application/pdf",
            UploadedAt = now.AddMinutes(1)
        });
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        var link = IncidentTestContext.Link(incident, feedback, IncidentLinkRole.Primary, now);
        incident.IncidentReportLinks.Add(link);
        var comment = new IncidentComment
        {
            IncidentCommentId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = currentUserId,
            Content = "Cần xử lý sớm",
            CreatedAt = now
        };
        var support = new IncidentSupport
        {
            IncidentSupportId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = currentUserId,
            CreatedAt = now
        };
        var subscription = new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = currentUserId,
            SourceType = IncidentSubscriptionSource.Manual,
            IsActive = true,
            CreatedAt = now
        };
        incident.IncidentComments.Add(comment);
        incident.IncidentSupports.Add(support);
        incident.IncidentSubscriptions.Add(subscription);
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(link);
        context.Comments.Add(comment);
        context.Supports.Add(support);
        context.Subscriptions.Add(subscription);

        var result = await new IncidentService(context.UnitOfWork)
            .GetPublicIncidentDetailAsync(incident.IncidentId, currentUserId);

        Assert.Equal("https://example.test/evidence.jpg", result.CoverImageUrl);
        Assert.Equal(result.CoverImageUrl, result.CoverImageThumbnailUrl);
        var media = Assert.Single(result.Media);
        Assert.Equal(feedback.FeedbackId, media.FeedbackId);
        Assert.Equal("image/jpeg", media.FileType);
        Assert.Equal(1, result.CommentCount);
        Assert.Equal(1, result.SupportCount);
        Assert.True(result.IsSupportedByCurrentUser);
        Assert.True(result.IsSubscribedByCurrentUser);
    }

    [Fact]
    public async Task IncidentInteractions_AllowCommentsAndIdempotentSupportOnPublicIncident()
    {
        var context = new IncidentTestContext();
        var now = DateTime.UtcNow;
        var user = context.AddActor(UserRole.SERVICEUSER, "Resident");
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        var link = IncidentTestContext.Link(incident, feedback, IncidentLinkRole.Primary, now);
        incident.IncidentReportLinks.Add(link);
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(link);
        var service = new IncidentService(context.UnitOfWork);

        var comment = await service.AddCommentAsync(
            incident.IncidentId,
            user.UserId,
            new UrbanService.BLL.Dtos.IncidentCommentCreateRequest
            {
                Content = "  Tôi cũng gặp vấn đề này  "
            });
        await service.SupportAsync(incident.IncidentId, user.UserId);
        await service.SupportAsync(incident.IncidentId, user.UserId);

        Assert.Equal("Tôi cũng gặp vấn đề này", comment.Content);
        Assert.Equal("Resident", comment.UserName);
        Assert.Single(context.Comments);
        Assert.Single(context.Supports);

        context.Comments.Single().User = user;
        var comments = await service.GetPublicCommentsAsync(incident.IncidentId, 1, 20);
        Assert.Equal(1, comments.TotalItems);
        Assert.Equal(comment.IncidentCommentId, Assert.Single(comments.Items).IncidentCommentId);

        await service.UnsupportAsync(incident.IncidentId, user.UserId);
        await service.UnsupportAsync(incident.IncidentId, user.UserId);
        Assert.Empty(context.Supports);
    }

    [Fact]
    public async Task IncidentComments_UpdateAndDeleteRequireCurrentUserOwnership()
    {
        var context = new IncidentTestContext();
        var now = DateTime.UtcNow;
        var owner = context.AddActor(UserRole.SERVICEUSER, "Comment owner");
        var otherUser = context.AddActor(UserRole.SERVICEUSER, "Other resident");
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), owner.UserId, now);
        feedback.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        var link = IncidentTestContext.Link(incident, feedback, IncidentLinkRole.Primary, now);
        incident.IncidentReportLinks.Add(link);
        var comment = new IncidentComment
        {
            IncidentCommentId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = owner.UserId,
            User = owner,
            Content = "Original comment",
            CreatedAt = now
        };
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(link);
        context.Comments.Add(comment);
        var service = new IncidentService(context.UnitOfWork);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateCommentAsync(
            incident.IncidentId,
            comment.IncidentCommentId,
            otherUser.UserId,
            new UrbanService.BLL.Dtos.IncidentCommentUpdateRequest { Content = "Not allowed" }));

        var updated = await service.UpdateCommentAsync(
            incident.IncidentId,
            comment.IncidentCommentId,
            owner.UserId,
            new UrbanService.BLL.Dtos.IncidentCommentUpdateRequest { Content = "  Updated comment  " });

        Assert.Equal("Updated comment", updated.Content);
        Assert.Equal("Updated comment", comment.Content);
        await Assert.ThrowsAsync<Exception>(() => service.DeleteCommentAsync(
            incident.IncidentId,
            comment.IncidentCommentId,
            otherUser.UserId));

        await service.DeleteCommentAsync(
            incident.IncidentId,
            comment.IncidentCommentId,
            owner.UserId);

        Assert.Empty(context.Comments);
    }

    [Fact]
    public async Task PublicIncidentList_ReturnsInteractionStateThumbnailAndTrendingOrder()
    {
        var context = new IncidentTestContext();
        var now = DateTime.UtcNow;
        var currentUser = context.AddActor(UserRole.SERVICEUSER, "Resident");
        var trendingFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddDays(-1));
        trendingFeedback.Status = FeedbackStatus.Verified;
        trendingFeedback.FeedbackAttachments.Add(new FeedbackAttachment
        {
            AttachmentId = 11,
            FeedbackId = trendingFeedback.FeedbackId,
            FileUrl = "https://res.cloudinary.com/demo/image/upload/v1/incidents/cover.jpg",
            FileType = "image/jpeg",
            UploadedAt = now.AddDays(-1)
        });
        var trendingIncident = IncidentTestContext.Incident(
            Guid.NewGuid(),
            trendingFeedback,
            now.AddDays(-1));
        trendingIncident.Status = IncidentStatus.Verified;
        var trendingLink = IncidentTestContext.Link(
            trendingIncident,
            trendingFeedback,
            IncidentLinkRole.Primary,
            now.AddDays(-1));
        trendingIncident.IncidentReportLinks.Add(trendingLink);
        var support = new IncidentSupport
        {
            IncidentSupportId = Guid.NewGuid(),
            IncidentId = trendingIncident.IncidentId,
            UserId = currentUser.UserId,
            User = currentUser,
            CreatedAt = now
        };
        var subscription = new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = trendingIncident.IncidentId,
            UserId = currentUser.UserId,
            User = currentUser,
            SourceType = IncidentSubscriptionSource.Manual,
            IsActive = true,
            CreatedAt = now
        };
        var comment = new IncidentComment
        {
            IncidentCommentId = Guid.NewGuid(),
            IncidentId = trendingIncident.IncidentId,
            UserId = currentUser.UserId,
            User = currentUser,
            Content = "Interested",
            CreatedAt = now
        };
        trendingIncident.IncidentSupports.Add(support);
        trendingIncident.IncidentSubscriptions.Add(subscription);
        trendingIncident.IncidentComments.Add(comment);

        var recentFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        recentFeedback.Status = FeedbackStatus.Verified;
        var recentIncident = IncidentTestContext.Incident(Guid.NewGuid(), recentFeedback, now);
        recentIncident.Status = IncidentStatus.Verified;
        var recentLink = IncidentTestContext.Link(
            recentIncident,
            recentFeedback,
            IncidentLinkRole.Primary,
            now);
        recentIncident.IncidentReportLinks.Add(recentLink);

        context.Incidents.AddRange([trendingIncident, recentIncident]);
        context.Feedbacks.AddRange([trendingFeedback, recentFeedback]);
        context.Links.AddRange([trendingLink, recentLink]);
        context.Supports.Add(support);
        context.Subscriptions.Add(subscription);
        context.Comments.Add(comment);

        var result = await new IncidentService(context.UnitOfWork).GetPublicIncidentsAsync(
            new UrbanService.BLL.Dtos.IncidentQueryParameters { Sort = "trending" },
            currentUser.UserId);

        Assert.Equal(2, result.TotalItems);
        var first = result.Items.First();
        Assert.Equal(trendingIncident.IncidentId, first.IncidentId);
        Assert.Equal(7, first.EngagementScore);
        Assert.True(first.IsSupportedByCurrentUser);
        Assert.True(first.IsSubscribedByCurrentUser);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/v1/incidents/cover.jpg", first.CoverImageUrl);
        Assert.Contains(
            "/image/upload/f_auto,q_auto,w_640,c_limit/",
            first.CoverImageThumbnailUrl,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_MovesCommentsAndDeduplicatesSupportsIntoTargetIncident()
    {
        var context = new IncidentTestContext();
        var now = DateTime.UtcNow;
        var manager = context.AddActor(UserRole.INTERACTIONMANAGER, "Manager");
        var sourceFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var targetFeedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-1));
        sourceFeedback.Status = FeedbackStatus.Verified;
        targetFeedback.Status = FeedbackStatus.Verified;
        var source = IncidentTestContext.Incident(Guid.NewGuid(), sourceFeedback, now);
        var target = IncidentTestContext.Incident(Guid.NewGuid(), targetFeedback, now.AddMinutes(-1));
        source.Status = IncidentStatus.Verified;
        target.Status = IncidentStatus.Verified;
        context.Incidents.AddRange([source, target]);
        context.Feedbacks.AddRange([sourceFeedback, targetFeedback]);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(source, sourceFeedback, IncidentLinkRole.Primary, now),
            IncidentTestContext.Link(target, targetFeedback, IncidentLinkRole.Primary, now.AddMinutes(-1))
        ]);
        context.AddManagerAreaAssignment(manager, source.Area);
        var sharedUserId = Guid.NewGuid();
        var sourceOnlyUserId = Guid.NewGuid();
        var comment = new IncidentComment
        {
            IncidentCommentId = Guid.NewGuid(),
            IncidentId = source.IncidentId,
            UserId = sharedUserId,
            Content = "Source comment",
            CreatedAt = now
        };
        context.Comments.Add(comment);
        context.Supports.AddRange(
        [
            new IncidentSupport
            {
                IncidentSupportId = Guid.NewGuid(),
                IncidentId = source.IncidentId,
                UserId = sharedUserId,
                CreatedAt = now
            },
            new IncidentSupport
            {
                IncidentSupportId = Guid.NewGuid(),
                IncidentId = source.IncidentId,
                UserId = sourceOnlyUserId,
                CreatedAt = now
            },
            new IncidentSupport
            {
                IncidentSupportId = Guid.NewGuid(),
                IncidentId = target.IncidentId,
                UserId = sharedUserId,
                CreatedAt = now.AddMinutes(-1)
            }
        ]);

        await new IncidentService(context.UnitOfWork).MergeAsync(
            source.IncidentId,
            new UrbanService.BLL.Dtos.MergeIncidentRequest
            {
                TargetIncidentId = target.IncidentId
            },
            manager.UserId);

        Assert.Equal(target.IncidentId, comment.IncidentId);
        Assert.Equal(2, context.Supports.Count);
        Assert.All(context.Supports, support => Assert.Equal(target.IncidentId, support.IncidentId));
        Assert.Equal(2, context.Supports.Select(support => support.UserId).Distinct().Count());
    }

    [Fact]
    public async Task IncidentInteractions_RejectIncidentWithoutPublicReport()
    {
        var context = new IncidentTestContext();
        var now = DateTime.UtcNow;
        var user = context.AddActor(UserRole.SERVICEUSER, "Resident");
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        var link = IncidentTestContext.Link(incident, feedback, IncidentLinkRole.Primary, now);
        incident.IncidentReportLinks.Add(link);
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(link);
        var service = new IncidentService(context.UnitOfWork);

        await Assert.ThrowsAsync<Exception>(() => service.AddCommentAsync(
            incident.IncidentId,
            user.UserId,
            new UrbanService.BLL.Dtos.IncidentCommentCreateRequest { Content = "Hidden" }));
        await Assert.ThrowsAsync<Exception>(() => service.SupportAsync(
            incident.IncidentId,
            user.UserId));

        Assert.Empty(context.Comments);
        Assert.Empty(context.Supports);
    }

    private sealed class IncidentTestContext
    {
        public IncidentTestContext()
        {
            ConfigureRepository(IncidentRepository, Incidents);
            ConfigureRepository(LinkRepository, Links);
            ConfigureRepository(SubscriptionRepository, Subscriptions);
            ConfigureRepository(CommentRepository, Comments);
            ConfigureRepository(SupportRepository, Supports);
            ConfigureRepository(EventRepository, Events);
            ConfigureRepository(FeedbackRepository, Feedbacks);
            ConfigureRepository(StatusHistoryRepository, StatusHistories);
            ConfigureRepository(ProviderReportRepository, ProviderReports);
            ConfigureRepository(AssignmentRepository, Assignments);
            ConfigureRepository(UserRepository, Users);
            ConfigureRepository(ManagerAreaAssignmentRepository, ManagerAreaAssignments);

            UnitOfWork.GetRepository<Incident>().Returns(IncidentRepository);
            UnitOfWork.GetRepository<IncidentReportLink>().Returns(LinkRepository);
            UnitOfWork.GetRepository<IncidentSubscription>().Returns(SubscriptionRepository);
            UnitOfWork.GetRepository<IncidentComment>().Returns(CommentRepository);
            UnitOfWork.GetRepository<IncidentSupport>().Returns(SupportRepository);
            UnitOfWork.GetRepository<IncidentEvent>().Returns(EventRepository);
            UnitOfWork.GetRepository<Feedback>().Returns(FeedbackRepository);
            UnitOfWork.GetRepository<FeedbackStatusHistory>().Returns(StatusHistoryRepository);
            UnitOfWork.GetRepository<FeedbackProviderReport>().Returns(ProviderReportRepository);
            UnitOfWork.GetRepository<StaffAreaAssignment>().Returns(AssignmentRepository);
            UnitOfWork.GetRepository<User>().Returns(UserRepository);
            UnitOfWork.GetRepository<ManagerAreaAssignment>().Returns(ManagerAreaAssignmentRepository);
            UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
            UnitOfWork.AcquireTransactionAdvisoryLockAsync(Arg.Any<long>()).Returns(Task.CompletedTask);

            LinkRepository.AddAsync(Arg.Any<IncidentReportLink>()).Returns(call =>
            {
                var link = call.Arg<IncidentReportLink>();
                link.Incident = Incidents.FirstOrDefault(item => item.IncidentId == link.IncidentId)
                    ?? link.Incident;
                link.Feedback = Feedbacks.FirstOrDefault(item => item.FeedbackId == link.FeedbackId)
                    ?? link.Feedback;
                Links.Add(link);
                return Task.CompletedTask;
            });
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public IGenericRepository<Incident> IncidentRepository { get; } = Substitute.For<IGenericRepository<Incident>>();
        public IGenericRepository<IncidentReportLink> LinkRepository { get; } = Substitute.For<IGenericRepository<IncidentReportLink>>();
        public IGenericRepository<IncidentSubscription> SubscriptionRepository { get; } = Substitute.For<IGenericRepository<IncidentSubscription>>();
        public IGenericRepository<IncidentComment> CommentRepository { get; } = Substitute.For<IGenericRepository<IncidentComment>>();
        public IGenericRepository<IncidentSupport> SupportRepository { get; } = Substitute.For<IGenericRepository<IncidentSupport>>();
        public IGenericRepository<IncidentEvent> EventRepository { get; } = Substitute.For<IGenericRepository<IncidentEvent>>();
        public IGenericRepository<Feedback> FeedbackRepository { get; } = Substitute.For<IGenericRepository<Feedback>>();
        public IGenericRepository<FeedbackStatusHistory> StatusHistoryRepository { get; } = Substitute.For<IGenericRepository<FeedbackStatusHistory>>();
        public IGenericRepository<FeedbackProviderReport> ProviderReportRepository { get; } = Substitute.For<IGenericRepository<FeedbackProviderReport>>();
        public IGenericRepository<StaffAreaAssignment> AssignmentRepository { get; } = Substitute.For<IGenericRepository<StaffAreaAssignment>>();
        public IGenericRepository<User> UserRepository { get; } = Substitute.For<IGenericRepository<User>>();
        public IGenericRepository<ManagerAreaAssignment> ManagerAreaAssignmentRepository { get; } = Substitute.For<IGenericRepository<ManagerAreaAssignment>>();

        public List<Incident> Incidents { get; } = [];
        public List<IncidentReportLink> Links { get; } = [];
        public List<IncidentSubscription> Subscriptions { get; } = [];
        public List<IncidentComment> Comments { get; } = [];
        public List<IncidentSupport> Supports { get; } = [];
        public List<IncidentEvent> Events { get; } = [];
        public List<Feedback> Feedbacks { get; } = [];
        public List<FeedbackStatusHistory> StatusHistories { get; } = [];
        public List<FeedbackProviderReport> ProviderReports { get; } = [];
        public List<StaffAreaAssignment> Assignments { get; } = [];
        public List<User> Users { get; } = [];
        public List<ManagerAreaAssignment> ManagerAreaAssignments { get; } = [];

        public User AddActor(string roleName, string fullName)
        {
            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = fullName,
                Email = $"{Guid.NewGuid()}@example.test",
                IsActive = true,
                Role = new Role
                {
                    RoleId = Random.Shared.Next(1, int.MaxValue),
                    RoleName = roleName
                }
            };
            Users.Add(user);
            return user;
        }

        public ManagerAreaAssignment AddManagerAreaAssignment(User manager, OperatingArea area)
        {
            var assignment = new ManagerAreaAssignment
            {
                ManagerAreaAssignmentId = Random.Shared.Next(1, int.MaxValue),
                ManagerUserId = manager.UserId,
                ManagerUser = manager,
                AreaId = area.AreaId,
                Area = area,
                CreatedByUserId = manager.UserId,
                CreatedByUser = manager,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            ManagerAreaAssignments.Add(assignment);
            return assignment;
        }

        public static Feedback Feedback(Guid feedbackId, Guid userId, DateTime createdAt)
        {
            return new Feedback
            {
                FeedbackId = feedbackId,
                UserId = userId,
                AreaId = 1,
                CategoryId = 1,
                Title = $"Feedback {feedbackId}",
                Description = "Mô tả sự vụ",
                LocationText = "Quận 1",
                Latitude = 10.762622m,
                Longitude = 106.660172m,
                Priority = "Medium",
                Status = "Submitted",
                SubmissionChannel = "Web",
                User = new User
                {
                    UserId = userId,
                    FullName = $"User {userId}",
                    Email = $"{userId}@example.test"
                },
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }

        public static Incident Incident(Guid incidentId, Feedback feedback, DateTime createdAt)
        {
            return new Incident
            {
                IncidentId = incidentId,
                AreaId = feedback.AreaId,
                CategoryId = feedback.CategoryId,
                Title = feedback.Title,
                Description = feedback.Description,
                LocationText = feedback.LocationText,
                Severity = IncidentSeverity.Medium,
                Status = feedback.Status,
                Area = new OperatingArea
                {
                    AreaId = feedback.AreaId,
                    AreaName = "Area 1",
                    AreaType = "Ward",
                    IsActive = true
                },
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }

        public static IncidentReportLink Link(
            Incident incident,
            Feedback feedback,
            string role,
            DateTime linkedAt)
        {
            return new IncidentReportLink
            {
                IncidentReportLinkId = Guid.NewGuid(),
                IncidentId = incident.IncidentId,
                Incident = incident,
                FeedbackId = feedback.FeedbackId,
                Feedback = feedback,
                LinkStatus = IncidentLinkStatus.Active,
                LinkMethod = IncidentLinkMethod.Created,
                LinkRole = role,
                LinkedAt = linkedAt
            };
        }

        public static User Staff(Guid userId, string fullName)
        {
            return new User
            {
                UserId = userId,
                FullName = fullName,
                Email = $"{userId}@example.test",
                IsActive = true,
                Role = new Role
                {
                    RoleId = 2,
                    RoleName = UserRole.SYSTEMSTAFF
                }
            };
        }

        public static StaffAreaAssignment Assignment(
            User staff,
            OperatingArea area,
            int? categoryId)
        {
            return new StaffAreaAssignment
            {
                StaffAreaAssignmentId = Random.Shared.Next(1, int.MaxValue),
                UserId = staff.UserId,
                User = staff,
                AreaId = area.AreaId,
                Area = area,
                CategoryId = categoryId,
                Category = categoryId.HasValue
                    ? new UrbanServiceCategory
                    {
                        CategoryId = categoryId.Value,
                        CategoryName = $"Category {categoryId.Value}",
                        IsActive = true
                    }
                    : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static void ConfigureRepository<T>(
            IGenericRepository<T> repository,
            List<T> entities)
            where T : class
        {
            repository.Entities.Returns(_ => entities.AsAsyncQueryable());
            repository.AddAsync(Arg.Any<T>()).Returns(call =>
            {
                entities.Add(call.Arg<T>());
                return Task.CompletedTask;
            });
            repository.AddRangeAsync(Arg.Any<IEnumerable<T>>()).Returns(call =>
            {
                entities.AddRange(call.Arg<IEnumerable<T>>());
                return Task.CompletedTask;
            });
            repository.When(instance => instance.Delete(Arg.Any<T>())).Do(call =>
            {
                entities.Remove(call.Arg<T>());
            });
        }
    }
}
