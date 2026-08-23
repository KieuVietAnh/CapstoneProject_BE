using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Data;
using UrbanService.DAL.Entities;
using UrbanService.Middlewares;
using Xunit;

namespace UrbanService.BLL.Tests;

public class FeedbackAdminDeleteTests
{
    [Fact]
    public async Task DeleteByManagementAsync_ExistingFeedback_DeletesAndSavesOnce()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: false);
        context.Feedbacks.Add(feedback);
        var service = CreateService(context);

        await service.DeleteByManagementAsync(feedback.FeedbackId);

        context.FeedbackRepository.Received(1).Delete(feedback);
        context.CandidateRepository.DidNotReceive()
            .DeleteRange(Arg.Any<IEnumerable<FeedbackDuplicateCandidate>>());
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task DeleteByManagementAsync_FeedbackIsCandidateParent_DeletesAllReferencesBeforeSingleSave()
    {
        var context = new DuplicateTestContext();
        var parent = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-1),
            isMaster: true);
        var pendingChild = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: false);
        var historicalChild = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow.AddSeconds(1),
            isMaster: false);
        context.Feedbacks.AddRange([parent, pendingChild, historicalChild]);
        var pendingCandidate = context.Candidate(pendingChild, parent);
        var historicalCandidate = context.Candidate(historicalChild, parent, status: "Rejected");
        var service = CreateService(context);

        await service.DeleteByManagementAsync(parent.FeedbackId);

        context.CandidateRepository.Received(1).DeleteRange(
            Arg.Is<IEnumerable<FeedbackDuplicateCandidate>>(items =>
                items.Count() == 2
                && items.Contains(pendingCandidate)
                && items.Contains(historicalCandidate)));
        context.FeedbackRepository.Received(1).Delete(parent);
        await context.UnitOfWork.Received(1).SaveAsync();

        Received.InOrder(() =>
        {
            context.CandidateRepository.DeleteRange(
                Arg.Is<IEnumerable<FeedbackDuplicateCandidate>>(items =>
                    items.Count() == 2
                    && items.Contains(pendingCandidate)
                    && items.Contains(historicalCandidate)));
            context.FeedbackRepository.Delete(parent);
            _ = context.UnitOfWork.SaveAsync();
        });
    }

    [Fact]
    public async Task DeleteByManagementAsync_MissingFeedback_DoesNotDeleteOrSave()
    {
        var context = new DuplicateTestContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.DeleteByManagementAsync(Guid.NewGuid()));

        Assert.Equal("Không tìm thấy feedback.", exception.Message);
        context.FeedbackRepository.DidNotReceive().Delete(Arg.Any<Feedback>());
        context.CandidateRepository.DidNotReceive()
            .DeleteRange(Arg.Any<IEnumerable<FeedbackDuplicateCandidate>>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task DeleteFeedback_MissingFeedback_MapsToBadRequestWithoutSaving()
    {
        var testContext = new DuplicateTestContext();
        var service = CreateService(testContext);
        var controller = CreateController(service);
        var feedbackId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            async _ => await controller.DeleteFeedback(feedbackId),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        using var responseJson = JsonDocument.Parse(await reader.ReadToEndAsync());
        Assert.Equal(
            "Không tìm thấy feedback.",
            responseJson.RootElement.GetProperty("msg").GetString());
        await testContext.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task DeleteFeedback_ActionReturnsNoContentAndCallsManagementDelete()
    {
        var feedbackService = Substitute.For<IFeedbackService>();
        var feedbackId = Guid.NewGuid();
        var controller = CreateController(feedbackService);

        var result = await controller.DeleteFeedback(feedbackId);

        Assert.IsType<NoContentResult>(result);
        await feedbackService.Received(1).DeleteByManagementAsync(feedbackId);
    }

    [Fact]
    public void DeleteFeedback_ActionRequiresSystemAdminAndGuidRoute()
    {
        var action = typeof(ManagementFeedbacksController)
            .GetMethod(nameof(ManagementFeedbacksController.DeleteFeedback))!;

        var authorize = Assert.Single(action
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(UserRole.SYSTEMADMIN, authorize.Roles);

        var httpDelete = Assert.Single(action
            .GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true)
            .Cast<HttpDeleteAttribute>());
        Assert.Equal("{feedbackId:guid}", httpDelete.Template);

        var responseTypes = action
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: true)
            .Cast<ProducesResponseTypeAttribute>();
        Assert.Contains(responseTypes, response => response.StatusCode == 204);
    }

    [Fact]
    public void FeedbackDeletionRelationships_KeepCascadeRestrictAndSetNullBehaviors()
    {
        var options = new DbContextOptionsBuilder<UrbanServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=urbanservice_model_test")
            .Options;
        using var dbContext = new UrbanServiceDbContext(options);

        var candidateEntity = dbContext.Model.FindEntityType(typeof(FeedbackDuplicateCandidate))!;
        var candidateFeedbackForeignKey = candidateEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Single().Name == nameof(FeedbackDuplicateCandidate.FeedbackId));
        var candidateParentForeignKey = candidateEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Single().Name == nameof(FeedbackDuplicateCandidate.PotentialParentFeedbackId));

        var feedbackEntity = dbContext.Model.FindEntityType(typeof(Feedback))!;
        var parentTicketForeignKey = feedbackEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Single().Name == nameof(Feedback.ParentTicketId));

        var attachmentEntity = dbContext.Model.FindEntityType(typeof(FeedbackAttachment))!;
        var attachmentFeedbackForeignKey = attachmentEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Single().Name == nameof(FeedbackAttachment.FeedbackId));

        Assert.Equal(DeleteBehavior.Cascade, candidateFeedbackForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, candidateParentForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, parentTicketForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, attachmentFeedbackForeignKey.DeleteBehavior);
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

    private static ManagementFeedbacksController CreateController(IFeedbackService feedbackService)
    {
        return new ManagementFeedbacksController(
            feedbackService,
            Substitute.For<IAreaAlertService>(),
            Substitute.For<IFeedbackDuplicateCandidateService>());
    }
}
