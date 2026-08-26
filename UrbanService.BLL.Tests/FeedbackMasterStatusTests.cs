using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class FeedbackMasterStatusTests
{
    [Theory]
    [InlineData(FeedbackStatus.Submitted)]
    [InlineData(FeedbackStatus.AiReviewed)]
    [InlineData(FeedbackStatus.Rejected)]
    [InlineData(FeedbackStatus.Cancelled)]
    public async Task UpdateStatus_RejectsInvalidStatusWhenMasterHasDuplicateChildren(
        string invalidStatus)
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var master = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Verified);
        var child = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            status: FeedbackStatus.AiReviewed,
            parentTicketId: master.FeedbackId);
        context.Feedbacks.AddRange([master, child]);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusByStaffOrAdminAsync(
            Guid.NewGuid(),
            master.FeedbackId,
            new UpdateFeedbackStatusRequest { Status = invalidStatus }));

        Assert.Equal(FeedbackStatus.Verified, master.Status);
        Assert.Equal(master.FeedbackId, child.ParentTicketId);
    }

    [Fact]
    public async Task UpdateStatus_BlocksWorkflowWhileDuplicateReviewIsPending()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var master = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Verified);
        var possibleDuplicate = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            status: FeedbackStatus.AiReviewed);
        context.Feedbacks.AddRange([master, possibleDuplicate]);
        context.Candidate(possibleDuplicate, master);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusByStaffOrAdminAsync(
            Guid.NewGuid(),
            possibleDuplicate.FeedbackId,
            new UpdateFeedbackStatusRequest { Status = FeedbackStatus.Verified }));

        Assert.Equal(FeedbackStatus.AiReviewed, possibleDuplicate.Status);
        Assert.Null(possibleDuplicate.ParentTicketId);
    }

    [Fact]
    public async Task UpdateStatus_BlocksConfirmedDuplicateFromSeparateWorkflow()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var master = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Verified);
        var duplicate = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            status: FeedbackStatus.AiReviewed,
            parentTicketId: master.FeedbackId);
        context.Feedbacks.AddRange([master, duplicate]);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusByStaffOrAdminAsync(
            Guid.NewGuid(),
            duplicate.FeedbackId,
            new UpdateFeedbackStatusRequest { Status = FeedbackStatus.Verified }));

        Assert.Equal(FeedbackStatus.AiReviewed, duplicate.Status);
    }

    [Fact]
    public async Task UpdateStatus_ForwardsOperationalStatusToCanonicalIncidentWorkflow()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.Verified);
        context.Feedbacks.Add(feedback);
        var actorUserId = Guid.NewGuid();
        var incidentService = Substitute.For<IIncidentService>();
        incidentService.UpdateStatusFromFeedbackAsync(
                feedback.FeedbackId,
                Arg.Is<UpdateIncidentStatusRequest>(request =>
                    request.Status == FeedbackStatus.Assigned &&
                    request.Note == "Assigned by staff"),
                actorUserId,
                Arg.Any<CancellationToken>())
            .Returns(new FeedbackStatusHistoryDto
            {
                FeedbackId = feedback.FeedbackId,
                ChangedByUserId = actorUserId,
                OldStatus = FeedbackStatus.Verified,
                NewStatus = FeedbackStatus.Assigned,
                Note = "Assigned by staff",
                ChangedAt = DateTime.UtcNow
            });
        var service = CreateService(context, incidentService);

        var result = await service.UpdateStatusByStaffOrAdminAsync(
            actorUserId,
            feedback.FeedbackId,
            new UpdateFeedbackStatusRequest
            {
                Status = FeedbackStatus.Assigned,
                Note = "Assigned by staff"
            });

        Assert.Equal(FeedbackStatus.Assigned, result.NewStatus);
        await incidentService.Received(1).UpdateStatusFromFeedbackAsync(
            feedback.FeedbackId,
            Arg.Any<UpdateIncidentStatusRequest>(),
            actorUserId,
            Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task Verify_ForwardsToCanonicalIncidentAndStartsLegacySla()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.AiReviewed);
        context.Feedbacks.Add(feedback);
        var actorUserId = Guid.NewGuid();
        var incidentService = Substitute.For<IIncidentService>();
        incidentService.UpdateStatusFromFeedbackAsync(
                feedback.FeedbackId,
                Arg.Is<UpdateIncidentStatusRequest>(request => request.Status == FeedbackStatus.Verified),
                actorUserId,
                Arg.Any<CancellationToken>())
            .Returns(new FeedbackStatusHistoryDto
            {
                FeedbackId = feedback.FeedbackId,
                ChangedByUserId = actorUserId,
                OldStatus = FeedbackStatus.AiReviewed,
                NewStatus = FeedbackStatus.Verified,
                Note = "Verified by staff",
                ChangedAt = DateTime.UtcNow
            });
        var slaRepository = Substitute.For<IGenericRepository<FeedbackSla>>();
        slaRepository.Entities.Returns(Array.Empty<FeedbackSla>().AsAsyncQueryable());
        context.UnitOfWork.GetRepository<FeedbackSla>().Returns(slaRepository);
        var slaService = Substitute.For<ISlaService>();
        var service = CreateService(context, incidentService, slaService);

        await service.VerifyFeedbackAsync(feedback.FeedbackId, actorUserId);

        await incidentService.Received(1).UpdateStatusFromFeedbackAsync(
            feedback.FeedbackId,
            Arg.Any<UpdateIncidentStatusRequest>(),
            actorUserId,
            Arg.Any<CancellationToken>());
        await slaService.Received(1).StartAsync(feedback.FeedbackId, actorUserId);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    private static FeedbackService CreateService(
        DuplicateTestContext context,
        IIncidentService? incidentService = null,
        ISlaService? slaService = null)
    {
        return new FeedbackService(
            context.UnitOfWork,
            Substitute.For<INotificationService>(),
            Substitute.For<IAiFeedbackReviewQueue>(),
            Substitute.For<IAiFeedbackDuplicateService>(),
            slaService ?? Substitute.For<ISlaService>(),
            incidentService ?? Substitute.For<IIncidentService>());
    }
}
