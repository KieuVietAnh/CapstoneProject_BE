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
    [Fact]
    public async Task Create_NewReport_DoesNotCreateIncidentBeforeManagerVerification()
    {
        var context = new DuplicateTestContext();
        var area = new OperatingArea
        {
            AreaId = 1,
            AreaName = "Area 1",
            AreaType = "Ward",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var areaRepository = Substitute.For<IGenericRepository<OperatingArea>>();
        areaRepository.Entities.Returns(new[] { area }.AsAsyncQueryable());
        context.UnitOfWork.GetRepository<OperatingArea>().Returns(areaRepository);
        context.FeedbackRepository.AddAsync(Arg.Any<Feedback>()).Returns(call =>
        {
            var feedback = call.Arg<Feedback>();
            feedback.Area = area;
            context.Feedbacks.Add(feedback);
            return Task.CompletedTask;
        });
        var incidentService = Substitute.For<IIncidentService>();
        var reviewQueue = Substitute.For<IAiFeedbackReviewQueue>();
        var service = new FeedbackService(
            context.UnitOfWork,
            Substitute.For<INotificationService>(),
            reviewQueue,
            Substitute.For<IAiFeedbackDuplicateService>(),
            Substitute.For<ISlaService>(),
            incidentService);
        var userId = Guid.NewGuid();

        var result = await service.CreateAsync(
            userId,
            new FeedbackCreateRequest
            {
                AreaId = area.AreaId,
                Title = "Ổ gà trên đường",
                Description = "Mặt đường xuất hiện ổ gà lớn",
                LocationText = "Phường 1"
            },
            Array.Empty<UploadedFeedbackAttachmentDto>());

        Assert.Equal(FeedbackStatus.Submitted, result.Status);
        Assert.Null(result.IncidentId);
        await incidentService.DidNotReceiveWithAnyArgs().StageReportInExistingIncidentAsync(
            default!,
            default,
            default,
            default,
            default);
        await reviewQueue.Received(1).EnqueueAsync(result.FeedbackId, userId);
    }

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
        context.TrackActiveIncident(master);
        context.TrackActiveIncident(child);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusByStaffOrAdminAsync(
            context.ManagerUserId,
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
        context.TrackActiveIncident(possibleDuplicate);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusByStaffOrAdminAsync(
            context.ManagerUserId,
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
        context.TrackActiveIncident(master);
        context.TrackActiveIncident(duplicate);
        var service = CreateService(context);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusByStaffOrAdminAsync(
            context.ManagerUserId,
            duplicate.FeedbackId,
            new UpdateFeedbackStatusRequest { Status = FeedbackStatus.Verified }));

        Assert.Equal(FeedbackStatus.AiReviewed, duplicate.Status);
    }

    [Fact]
    public async Task UpdateStatus_RejectsOperationalStatusToPreventWorkflowBypass()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.Verified);
        context.Feedbacks.Add(feedback);
        context.TrackActiveIncident(feedback);
        var actorUserId = context.ManagerUserId;
        var incidentService = Substitute.For<IIncidentService>();
        var service = CreateService(context, incidentService);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusByStaffOrAdminAsync(
            actorUserId,
            feedback.FeedbackId,
            new UpdateFeedbackStatusRequest
            {
                Status = FeedbackStatus.Assigned,
                Note = "Assigned by manager"
            }));

        Assert.Equal(FeedbackStatus.Verified, feedback.Status);
        await incidentService.DidNotReceive().UpdateStatusFromFeedbackAsync(
            feedback.FeedbackId,
            Arg.Any<UpdateIncidentStatusRequest>(),
            actorUserId,
            Arg.Any<CancellationToken>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task Verify_UnlinkedReportCreatesVerifiedIncidentAndStartsLegacySla()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.AiReviewed);
        context.Feedbacks.Add(feedback);
        var actorUserId = context.ManagerUserId;
        var incidentService = Substitute.For<IIncidentService>();
        incidentService.VerifyReportAsync(
                feedback.FeedbackId,
                actorUserId,
                Arg.Is<string>(note => note.Contains("xác nhận phản ánh")),
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

        await incidentService.Received(1).VerifyReportAsync(
            feedback.FeedbackId,
            actorUserId,
            Arg.Any<string>(),
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
