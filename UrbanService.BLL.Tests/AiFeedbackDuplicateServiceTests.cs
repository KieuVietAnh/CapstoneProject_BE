using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using Xunit;

namespace UrbanService.BLL.Tests;

public class AiFeedbackDuplicateServiceTests
{
    [Fact]
    public async Task NoNearbyMaster_PromotesFeedbackToMaster()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: false);
        context.Feedbacks.Add(feedback);

        var aiClient = Substitute.For<IAiClient>();
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(feedback, Guid.NewGuid());

        Assert.True(feedback.IsMasterTicket);
        Assert.Null(feedback.ParentTicketId);
        Assert.Empty(context.Candidates);
        context.UnitOfWork.Received(1).BeginTransaction();
        await context.UnitOfWork.Received(1).AcquireTransactionAdvisoryLockAsync(
            0x4455504C00000000L | (uint)feedback.AreaId);
        context.UnitOfWork.Received(1).CommitTransaction();
        context.UnitOfWork.DidNotReceive().RollBack();
    }

    [Fact]
    public async Task AiSaysNotDuplicate_PromotesFeedbackToMaster()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var master = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-5),
            isMaster: true);
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false);
        context.Feedbacks.AddRange([master, feedback]);

        var aiClient = AiReturning("""
            {"isDuplicate":false,"parentFeedbackId":null,"confidenceScore":0.1,"reason":"Khong trung"}
            """);
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(feedback, Guid.NewGuid());

        Assert.True(feedback.IsMasterTicket);
        Assert.Empty(context.Candidates);
    }

    [Fact]
    public async Task PendingB_IsNotOfferedAsParentWhenClassifyingC()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-10),
            isMaster: true,
            status: FeedbackStatus.Submitted);
        var b = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-5),
            isMaster: false);
        var c = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false);
        context.Feedbacks.AddRange([a, b, c]);
        context.Candidate(b, a);

        string? capturedPrompt = null;
        var aiClient = Substitute.For<IAiClient>();
        aiClient.ChatAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capturedPrompt = call.ArgAt<string>(0);
                return Task.FromResult(
                    $$"""{"isDuplicate":true,"parentFeedbackId":"{{a.FeedbackId}}","confidenceScore":0.95,"reason":"Trung phan anh A"}""");
            });
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(c, Guid.NewGuid());

        Assert.NotNull(capturedPrompt);
        Assert.Contains(a.FeedbackId.ToString(), capturedPrompt);
        Assert.DoesNotContain(b.FeedbackId.ToString(), capturedPrompt);
        var cCandidate = Assert.Single(context.Candidates.Where(candidate => candidate.FeedbackId == c.FeedbackId));
        Assert.Equal(a.FeedbackId, cCandidate.PotentialParentFeedbackId);
        Assert.Equal("Pending", cCandidate.Status);
        Assert.False(c.IsMasterTicket);
        Assert.Null(c.ParentTicketId);
    }

    [Fact]
    public async Task ExistingActiveCandidate_PreventsCompetingParentCandidate()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var d = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-8), isMaster: true);
        var c = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, d, c]);
        var existing = context.Candidate(c, a);

        var aiClient = AiReturning(
            $$"""{"isDuplicate":true,"parentFeedbackId":"{{d.FeedbackId}}","confidenceScore":0.93,"reason":"Trung phan anh D"}""");
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(c, Guid.NewGuid());

        var activeCandidates = context.Candidates
            .Where(candidate =>
                candidate.FeedbackId == c.FeedbackId &&
                (candidate.Status == "Pending" || candidate.Status == "Confirmed"))
            .ToList();
        Assert.Single(activeCandidates);
        Assert.Same(existing, activeCandidates[0]);
    }

    [Fact]
    public async Task AiFailure_LeavesFeedbackUnresolvedForRetry()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var a = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt.AddMinutes(-10), isMaster: true);
        var feedback = DuplicateTestContext.Feedback(Guid.NewGuid(), createdAt, isMaster: false);
        context.Feedbacks.AddRange([a, feedback]);

        var aiClient = Substitute.For<IAiClient>();
        aiClient.ChatAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("AI unavailable")));
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(feedback, Guid.NewGuid());

        Assert.False(feedback.IsMasterTicket);
        Assert.Null(feedback.ParentTicketId);
        Assert.Empty(context.Candidates);
        context.UnitOfWork.Received(1).BeginTransaction();
        await context.UnitOfWork.Received(1).AcquireTransactionAdvisoryLockAsync(
            0x4455504C00000000L | (uint)feedback.AreaId);
        context.UnitOfWork.Received(1).RollBack();
        context.UnitOfWork.DidNotReceive().CommitTransaction();
    }

    [Fact]
    public async Task AdvisoryLockFailure_RollsBackAndLeavesFeedbackForRetry()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: false,
            areaId: 27);
        context.Feedbacks.Add(feedback);
        context.UnitOfWork.AcquireTransactionAdvisoryLockAsync(Arg.Any<long>())
            .Returns(Task.FromException(new InvalidOperationException("database unavailable")));

        var aiClient = Substitute.For<IAiClient>();
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(feedback, Guid.NewGuid());

        Assert.False(feedback.IsMasterTicket);
        Assert.Empty(context.Candidates);
        context.UnitOfWork.Received(1).BeginTransaction();
        await context.UnitOfWork.Received(1).AcquireTransactionAdvisoryLockAsync(
            0x4455504C00000000L | 27u);
        context.UnitOfWork.Received(1).RollBack();
        context.UnitOfWork.DidNotReceive().CommitTransaction();
        await aiClient.DidNotReceiveWithAnyArgs().ChatAsync(
            default!,
            default,
            default,
            default);
    }

    [Fact]
    public async Task NewerFeedback_DefersUntilOlderUnresolvedFeedbackIsClassified()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var older = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddSeconds(-1),
            isMaster: false);
        var newer = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false);
        context.Feedbacks.AddRange([older, newer]);

        var aiClient = Substitute.For<IAiClient>();
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(newer, Guid.NewGuid());

        Assert.False(newer.IsMasterTicket);
        Assert.Empty(context.Candidates);
        context.UnitOfWork.Received(1).CommitTransaction();
        context.UnitOfWork.DidNotReceive().RollBack();
        await aiClient.DidNotReceiveWithAnyArgs().ChatAsync(
            default!,
            default,
            default,
            default);
    }

    [Fact]
    public async Task MasterOlderThanLookbackWindow_IsNotOfferedToAi()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var oldMaster = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddDays(-8),
            isMaster: true);
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false);
        context.Feedbacks.AddRange([oldMaster, feedback]);

        var aiClient = Substitute.For<IAiClient>();
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(feedback, Guid.NewGuid());

        Assert.True(feedback.IsMasterTicket);
        Assert.Empty(context.Candidates);
        await aiClient.DidNotReceiveWithAnyArgs().ChatAsync(
            default!,
            default,
            default,
            default);
    }

    [Fact]
    public async Task UnresolvedFeedbackOlderThanLookbackWindow_DoesNotDeferClassification()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var oldUnresolved = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddDays(-8),
            isMaster: false);
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false);
        context.Feedbacks.AddRange([oldUnresolved, feedback]);

        var aiClient = Substitute.For<IAiClient>();
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(feedback, Guid.NewGuid());

        Assert.True(feedback.IsMasterTicket);
        Assert.Empty(context.Candidates);
        await aiClient.DidNotReceiveWithAnyArgs().ChatAsync(
            default!,
            default,
            default,
            default);
    }

    [Fact]
    public async Task MasterOutsideTwoHundredMeters_IsNotOfferedToAi()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var distantMaster = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-5),
            isMaster: true,
            latitude: 10.7655m,
            longitude: 106.660172m);
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false);
        context.Feedbacks.AddRange([distantMaster, feedback]);

        var aiClient = Substitute.For<IAiClient>();
        var service = CreateService(context, aiClient);

        await service.CheckAndLinkDuplicateAsync(feedback, Guid.NewGuid());

        Assert.True(feedback.IsMasterTicket);
        Assert.Empty(context.Candidates);
        await aiClient.DidNotReceiveWithAnyArgs().ChatAsync(
            default!,
            default,
            default,
            default);
    }

    private static AiFeedbackDuplicateService CreateService(
        DuplicateTestContext context,
        IAiClient aiClient)
    {
        var configuration = Substitute.For<IConfiguration>();
        return new AiFeedbackDuplicateService(
            context.UnitOfWork,
            aiClient,
            configuration,
            NullLogger<AiFeedbackDuplicateService>.Instance);
    }

    private static IAiClient AiReturning(string response)
    {
        var aiClient = Substitute.For<IAiClient>();
        aiClient.ChatAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<string>?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        return aiClient;
    }
}
