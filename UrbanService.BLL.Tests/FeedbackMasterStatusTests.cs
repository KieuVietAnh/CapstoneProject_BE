using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
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

    private static FeedbackService CreateService(DuplicateTestContext context)
    {
        return new FeedbackService(
            context.UnitOfWork,
            Substitute.For<INotificationService>(),
            Substitute.For<IAiFeedbackReviewQueue>(),
            Substitute.For<IAiFeedbackDuplicateService>(),
            Substitute.For<ISlaService>(),
            Substitute.For<IIncidentService>());
    }
}
