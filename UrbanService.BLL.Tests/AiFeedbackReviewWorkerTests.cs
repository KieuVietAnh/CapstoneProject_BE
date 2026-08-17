using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using UrbanService.BackgroundServices;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.AI;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class AiFeedbackReviewWorkerTests
{
    [Fact]
    public async Task SubmittedFeedback_DeferredDuplicateStillRunsAiReview()
    {
        var context = new DuplicateTestContext();
        var createdAt = DateTime.UtcNow;
        var older = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt.AddMinutes(-1),
            isMaster: false,
            status: FeedbackStatus.Submitted);
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            createdAt,
            isMaster: false,
            status: FeedbackStatus.Submitted);
        context.Feedbacks.AddRange([older, feedback]);

        var duplicateService = Substitute.For<IAiFeedbackDuplicateService>();
        var analysisService = Substitute.For<IAiFeedbackAnalysisService>();
        using var cancellation = new CancellationTokenSource();
        analysisService.AnalyzeFeedbackAsync(
                feedback.FeedbackId,
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromResult(new AiAnalysisResponseDto { FeedbackId = feedback.FeedbackId });
            });

        var queue = new AiFeedbackReviewQueue();
        await queue.EnqueueAsync(feedback.FeedbackId, feedback.UserId);
        var worker = CreateWorker(context, duplicateService, analysisService, queue);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InvokeProcessQueueAsync(worker, cancellation.Token));

        await analysisService.Received(1).AnalyzeFeedbackAsync(
            feedback.FeedbackId,
            feedback.UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiReviewedFeedback_WithUnresolvedDuplicateDoesNotRunAiReviewAgain()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: false,
            status: FeedbackStatus.AiReviewed);
        context.Feedbacks.Add(feedback);

        var duplicateService = Substitute.For<IAiFeedbackDuplicateService>();
        var analysisService = Substitute.For<IAiFeedbackAnalysisService>();
        using var cancellation = new CancellationTokenSource();
        duplicateService.CheckAndLinkDuplicateAsync(
                feedback,
                Arg.Any<Guid>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        var queue = new AiFeedbackReviewQueue();
        await queue.EnqueueAsync(feedback.FeedbackId, feedback.UserId);
        var worker = CreateWorker(context, duplicateService, analysisService, queue);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InvokeProcessQueueAsync(worker, cancellation.Token));

        await analysisService.DidNotReceiveWithAnyArgs().AnalyzeFeedbackAsync(
            default,
            default,
            default);
    }

    private static AiFeedbackReviewWorker CreateWorker(
        DuplicateTestContext context,
        IAiFeedbackDuplicateService duplicateService,
        IAiFeedbackAnalysisService analysisService,
        IAiFeedbackReviewQueue queue)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => context.UnitOfWork);
        services.AddScoped<IAiFeedbackDuplicateService>(_ => duplicateService);
        services.AddScoped<IAiFeedbackAnalysisService>(_ => analysisService);
        var provider = services.BuildServiceProvider();

        var configuration = Substitute.For<IConfiguration>();
        configuration["AI:ReviewFailureRetryDelayMinutes"].Returns("1");

        return new AiFeedbackReviewWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            queue,
            NullLogger<AiFeedbackReviewWorker>.Instance,
            configuration);
    }

    private static Task InvokeProcessQueueAsync(
        AiFeedbackReviewWorker worker,
        CancellationToken cancellationToken)
    {
        var method = typeof(AiFeedbackReviewWorker).GetMethod(
            "ProcessQueueAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (Task)method.Invoke(worker, [cancellationToken])!;
    }
}