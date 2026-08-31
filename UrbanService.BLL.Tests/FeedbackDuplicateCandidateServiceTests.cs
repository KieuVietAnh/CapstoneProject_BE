using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Entities;
using Xunit;

namespace UrbanService.BLL.Tests;

public class FeedbackDuplicateCandidateServiceTests
{
    [Fact]
    public async Task Confirm_LinksChildDirectlyToEligibleMaster()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Verified);
        var b = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            status: FeedbackStatus.AiReviewed);
        context.Feedbacks.AddRange([a, b]);
        var candidate = context.Candidate(b, a);
        var notificationService = Substitute.For<INotificationService>();
        var incidentService = Substitute.For<IIncidentService>();
        var service = new FeedbackDuplicateCandidateService(
            context.UnitOfWork,
            notificationService,
            incidentService);
        var managerUserId = context.ManagerUserId;

        await service.ConfirmAsync(candidate.DuplicateCandidateId, managerUserId);

        Assert.Equal(a.FeedbackId, b.ParentTicketId);
        Assert.False(b.IsMasterTicket);
        Assert.True(a.IsMasterTicket);
        Assert.Equal("Confirmed", candidate.Status);
        Assert.Equal(managerUserId, candidate.ReviewedByUserId);
        context.UnitOfWork.Received(1).CommitTransaction();
        await incidentService.Received(1).RelinkConfirmedDuplicateAsync(
            b,
            a,
            managerUserId,
            candidate.ConfidenceScore,
            candidate.Reason,
            Arg.Any<CancellationToken>());
        await notificationService.Received(1).SendAsync(
            b.UserId,
            "Phản ánh đã được ghi nhận vào sự vụ hiện có",
            Arg.Is<string>(message => message.Contains("thông tin bổ sung") && message.Contains("vẫn được lưu giữ")),
            Arg.Any<string>(),
            $"/community/feed/{a.FeedbackId}");
    }

    [Fact]
    public async Task CandidateDetail_MixedCaseManagerRole_ExposesCurrentAndSuggestedIncidents()
    {
        var context = new DuplicateTestContext();
        context.Users.Single(user => user.UserId == context.ManagerUserId)
            .Role.RoleName = "InteractionManager";
        var createdAt = DateTime.UtcNow;
        var parent = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var report = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        var currentIncidentId = Guid.NewGuid();
        var suggestedIncidentId = Guid.NewGuid();
        report.IncidentReportLinks.Add(ActiveLink(currentIncidentId, report));
        parent.IncidentReportLinks.Add(ActiveLink(suggestedIncidentId, parent));
        context.Feedbacks.AddRange([parent, report]);
        var candidate = context.Candidate(report, parent);
        var service = CreateService(context);

        var result = await service.GetCandidateDetailAsync(
            candidate.DuplicateCandidateId,
            context.ManagerUserId);

        Assert.Equal(currentIncidentId, result.IncidentId);
        Assert.Equal(currentIncidentId, result.CurrentIncidentId);
        Assert.Equal(suggestedIncidentId, result.SuggestedIncidentId);
        Assert.False(result.AreInSameIncident);
    }

    [Fact]
    public async Task CandidateDetail_UnlinkedReport_IsVisibleToManagerForItsArea()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var parent = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Verified);
        var report = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            status: FeedbackStatus.AiReviewed);
        context.Feedbacks.AddRange([parent, report]);
        var candidate = context.Candidate(report, parent);
        var service = CreateService(context);

        var result = await service.GetCandidateDetailAsync(
            candidate.DuplicateCandidateId,
            context.ManagerUserId);

        Assert.Null(result.CurrentIncidentId);
        Assert.Equal(report.FeedbackId, result.FeedbackId);
    }

    [Fact]
    public void Controller_ExposesIncidentMatchRouteAlongsideLegacyRoute()
    {
        var routes = typeof(StaffFeedbackDuplicatesController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(attribute => attribute.Template)
            .ToList();

        Assert.Contains("api/staff/feedback-duplicates", routes);
        Assert.Contains("api/management/incident-match-candidates", routes);
    }

    [Fact]
    public async Task Confirm_RejectsOtherPendingCandidatesForSameChild()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var d = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-8), isMaster: true);
        var b = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, d, b]);
        var selected = context.Candidate(b, a);
        var competing = context.Candidate(b, d);
        var service = CreateService(context);
        var managerUserId = context.ManagerUserId;

        await service.ConfirmAsync(selected.DuplicateCandidateId, managerUserId);

        Assert.Equal("Confirmed", selected.Status);
        Assert.Equal("Rejected", competing.Status);
        Assert.Equal(managerUserId, competing.ReviewedByUserId);
        Assert.Equal(a.FeedbackId, b.ParentTicketId);
    }

    [Fact]
    public async Task Confirm_WhenAlreadyConfirmed_IsIdempotent()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var parent = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var report = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            parentTicketId: parent.FeedbackId);
        context.Feedbacks.AddRange([parent, report]);
        var candidate = context.Candidate(report, parent, status: "Confirmed");
        var notificationService = Substitute.For<INotificationService>();
        var incidentService = Substitute.For<IIncidentService>();
        var service = new FeedbackDuplicateCandidateService(
            context.UnitOfWork,
            notificationService,
            incidentService);

        var result = await service.ConfirmAsync(
            candidate.DuplicateCandidateId,
            context.ManagerUserId);

        Assert.Equal("Confirmed", result.Status);
        await incidentService.DidNotReceiveWithAnyArgs().RelinkConfirmedDuplicateAsync(
            default!, default!, default, default, default);
        await notificationService.DidNotReceiveWithAnyArgs().SendAsync(
            default, default!, default!, default!, default!);
        context.UnitOfWork.Received(1).CommitTransaction();
    }

    [Fact]
    public async Task Confirm_RejectsParentThatIsNotPublicYet()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Submitted);
        var b = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, b]);
        var candidate = context.Candidate(b, a);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(
            () => service.ConfirmAsync(candidate.DuplicateCandidateId, context.ManagerUserId));

        Assert.Null(b.ParentTicketId);
        Assert.Equal("Pending", candidate.Status);
        context.UnitOfWork.Received(1).RollBack();
    }

    [Fact]
    public async Task Confirm_AllowsClosedPublicMaster()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Closed);
        var b = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, b]);
        var candidate = context.Candidate(b, a);
        var service = CreateService(context);

        await service.ConfirmAsync(candidate.DuplicateCandidateId, context.ManagerUserId);

        Assert.Equal(a.FeedbackId, b.ParentTicketId);
        Assert.Equal("Confirmed", candidate.Status);
        Assert.True(a.IsMasterTicket);
        Assert.False(b.IsMasterTicket);
    }

    [Fact]
    public async Task Confirm_RejectsPendingFeedbackBAsParentOfC()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-15), isMaster: true);
        var b = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: false);
        var c = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, b, c]);
        context.Candidate(b, a);
        var invalidCandidate = context.Candidate(c, b);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(
            () => service.ConfirmAsync(
                invalidCandidate.DuplicateCandidateId,
                context.ManagerUserId));

        Assert.Null(c.ParentTicketId);
        Assert.Equal("Pending", invalidCandidate.Status);
    }

    [Fact]
    public async Task Confirm_RejectsMasterFromAnotherArea()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            areaId: 1);
        var b = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            areaId: 2);
        context.Feedbacks.AddRange([a, b]);
        var candidate = context.Candidate(b, a);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(
            () => service.ConfirmAsync(candidate.DuplicateCandidateId, context.ManagerUserId));

        Assert.Null(b.ParentTicketId);
        Assert.Equal("Pending", candidate.Status);
    }

    [Fact]
    public async Task Confirm_RejectsWhenChildAlreadyHasConfirmedCompetitor()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var d = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-8), isMaster: true);
        var b = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, d, b]);
        var selected = context.Candidate(b, a);
        context.Candidate(b, d, status: "Confirmed");
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(
            () => service.ConfirmAsync(selected.DuplicateCandidateId, context.ManagerUserId));

        Assert.Null(b.ParentTicketId);
        Assert.Equal("Pending", selected.Status);
    }

    [Fact]
    public async Task Reject_LastActiveCandidate_PromotesChildToMaster()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var b = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, b]);
        var candidate = context.Candidate(b, a);
        var service = CreateService(context);

        await service.RejectAsync(candidate.DuplicateCandidateId, context.ManagerUserId);

        Assert.Equal("Rejected", candidate.Status);
        Assert.True(b.IsMasterTicket);
        Assert.Null(b.ParentTicketId);
    }

    [Fact]
    public async Task Reject_WhenAnotherCandidateIsPending_KeepsChildUnresolved()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var d = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-8), isMaster: true);
        var b = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, d, b]);
        var rejected = context.Candidate(b, a);
        context.Candidate(b, d);
        var service = CreateService(context);

        await service.RejectAsync(rejected.DuplicateCandidateId, context.ManagerUserId);

        Assert.Equal("Rejected", rejected.Status);
        Assert.False(b.IsMasterTicket);
        Assert.Null(b.ParentTicketId);
    }

    [Fact]
    public async Task Reject_WhenAlreadyRejected_IsIdempotent()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var parent = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var report = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: true);
        context.Feedbacks.AddRange([parent, report]);
        var candidate = context.Candidate(report, parent, status: "Rejected");
        var service = CreateService(context);

        var result = await service.RejectAsync(
            candidate.DuplicateCandidateId,
            context.ManagerUserId);

        Assert.Equal("Rejected", result.Status);
        Assert.True(report.IsMasterTicket);
        Assert.Null(report.ParentTicketId);
        context.UnitOfWork.Received(1).CommitTransaction();
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    private static FeedbackDuplicateCandidateService CreateService(DuplicateTestContext context)
    {
        return new FeedbackDuplicateCandidateService(
            context.UnitOfWork,
            Substitute.For<INotificationService>(),
            Substitute.For<IIncidentService>());
    }

    private static IncidentReportLink ActiveLink(Guid incidentId, Feedback feedback)
    {
        var incident = new Incident
        {
            IncidentId = incidentId,
            AreaId = feedback.AreaId,
            Title = feedback.Title,
            LocationText = feedback.LocationText,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt
        };

        return new IncidentReportLink
        {
            IncidentReportLinkId = Guid.NewGuid(),
            IncidentId = incidentId,
            Incident = incident,
            FeedbackId = feedback.FeedbackId,
            Feedback = feedback,
            LinkStatus = IncidentLinkStatus.Active,
            LinkMethod = IncidentLinkMethod.Created,
            LinkRole = IncidentLinkRole.Primary,
            LinkedAt = feedback.CreatedAt
        };
    }
}
