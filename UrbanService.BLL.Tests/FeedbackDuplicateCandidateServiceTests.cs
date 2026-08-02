using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
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
        var service = new FeedbackDuplicateCandidateService(context.UnitOfWork, notificationService);
        var staffUserId = Guid.NewGuid();

        await service.ConfirmAsync(candidate.DuplicateCandidateId, staffUserId);

        Assert.Equal(a.FeedbackId, b.ParentTicketId);
        Assert.False(b.IsMasterTicket);
        Assert.True(a.IsMasterTicket);
        Assert.Equal("Confirmed", candidate.Status);
        Assert.Equal(staffUserId, candidate.ReviewedByUserId);
        context.UnitOfWork.Received(1).CommitTransaction();
        await notificationService.Received(1).SendAsync(
            b.UserId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            $"/community/feed/{a.FeedbackId}");
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
        var staffUserId = Guid.NewGuid();

        await service.ConfirmAsync(selected.DuplicateCandidateId, staffUserId);

        Assert.Equal("Confirmed", selected.Status);
        Assert.Equal("Rejected", competing.Status);
        Assert.Equal(staffUserId, competing.ReviewedByUserId);
        Assert.Equal(a.FeedbackId, b.ParentTicketId);
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
            () => service.ConfirmAsync(candidate.DuplicateCandidateId, Guid.NewGuid()));

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

        await service.ConfirmAsync(candidate.DuplicateCandidateId, Guid.NewGuid());

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
            () => service.ConfirmAsync(invalidCandidate.DuplicateCandidateId, Guid.NewGuid()));

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
            () => service.ConfirmAsync(candidate.DuplicateCandidateId, Guid.NewGuid()));

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
            () => service.ConfirmAsync(selected.DuplicateCandidateId, Guid.NewGuid()));

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

        await service.RejectAsync(candidate.DuplicateCandidateId, Guid.NewGuid());

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

        await service.RejectAsync(rejected.DuplicateCandidateId, Guid.NewGuid());

        Assert.Equal("Rejected", rejected.Status);
        Assert.False(b.IsMasterTicket);
        Assert.Null(b.ParentTicketId);
    }

    private static FeedbackDuplicateCandidateService CreateService(DuplicateTestContext context)
    {
        return new FeedbackDuplicateCandidateService(
            context.UnitOfWork,
            Substitute.For<INotificationService>());
    }
}
