using NSubstitute;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class FeedbackManagementUpdateTests
{
    [Theory]
    [InlineData(FeedbackStatus.Submitted)]
    [InlineData(FeedbackStatus.AiReviewed)]
    public async Task Update_UnlinkedPreVerificationReport_SavesClassificationWithoutCreatingIncident(string status)
    {
        var (context, feedback, service, incidentService) = CreateContext(status);

        var result = await service.UpdateByStaffAsync(context.ManagerUserId, feedback.FeedbackId,
            new StaffFeedbackUpdateRequest { CategoryId = 10, Priority = "High", Severity = " critical " });

        Assert.Equal(10, feedback.CategoryId);
        Assert.Equal("High", feedback.Priority);
        Assert.Equal(IncidentSeverity.Critical, feedback.Severity);
        Assert.Equal(10, result.CategoryId);
        Assert.Equal("High", result.Priority);
        Assert.Equal(IncidentSeverity.Critical, result.Severity);
        Assert.Equal(status, result.Status);
        Assert.Null(result.IncidentId);
        Assert.Empty(context.IncidentReportLinks);
        Assert.Empty(context.Incidents);
        Assert.Empty(feedback.FeedbackStatusHistories);
        Assert.Empty(incidentService.ReceivedCalls());
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Theory]
    [InlineData(" low ", IncidentSeverity.Low)]
    [InlineData("MEDIUM", IncidentSeverity.Medium)]
    [InlineData("high", IncidentSeverity.High)]
    [InlineData(null, IncidentSeverity.Medium)]
    public async Task Update_NormalizesSeverityOrPreservesItWhenOmitted(string? input, string expected)
    {
        var (context, feedback, service, _) = CreateContext();

        var result = await service.UpdateByStaffAsync(context.ManagerUserId, feedback.FeedbackId,
            new StaffFeedbackUpdateRequest { Severity = input });

        Assert.Equal(expected, feedback.Severity);
        Assert.Equal(expected, result.Severity);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Urgent")]
    public async Task Update_InvalidSeverityRejectsBeforeChangingClassification(string severity)
    {
        var (context, feedback, service, _) = CreateContext();

        var exception = await Assert.ThrowsAsync<Exception>(() => service.UpdateByStaffAsync(
            context.ManagerUserId, feedback.FeedbackId,
            new StaffFeedbackUpdateRequest { CategoryId = 10, Priority = "High", Severity = severity }));

        Assert.Contains("Severity", exception.Message);
        Assert.Null(feedback.CategoryId);
        Assert.Equal("Low", feedback.Priority);
        Assert.Equal(IncidentSeverity.Medium, feedback.Severity);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Theory]
    [InlineData(FeedbackStatus.Verified)]
    [InlineData(FeedbackStatus.Assigned)]
    [InlineData(FeedbackStatus.Rejected)]
    [InlineData(FeedbackStatus.Cancelled)]
    [InlineData(FeedbackStatus.Closed)]
    public async Task Update_UnlinkedReportOutsidePreVerificationRemainsForbidden(string status)
    {
        var (context, feedback, service, _) = CreateContext(status);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateByStaffAsync(
            context.ManagerUserId, feedback.FeedbackId, new StaffFeedbackUpdateRequest { Priority = "High" }));

        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_ManagerCannotEditOutsideCurrentOrRequestedWard(bool changeTargetArea)
    {
        var (context, feedback, service, _) = CreateContext();
        if (!changeTargetArea) feedback.AreaId = 3;

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateByStaffAsync(
            context.ManagerUserId, feedback.FeedbackId,
            new StaffFeedbackUpdateRequest { AreaId = changeTargetArea ? 3 : null, Priority = "High" }));

        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Theory]
    [InlineData(UserRole.SYSTEMSTAFF)]
    [InlineData(UserRole.SYSTEMADMIN)]
    [InlineData(UserRole.SERVICEUSER)]
    public async Task Update_UnlinkedReportRequiresManagerRole(string role)
    {
        var (context, feedback, service, _) = CreateContext();
        var actor = context.AddActor(role);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateByStaffAsync(
            actor.UserId, feedback.FeedbackId, new StaffFeedbackUpdateRequest { Priority = "High" }));

        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_ActiveLinkRequiresPrimaryAndUnmergedIncident(bool merged)
    {
        var (context, feedback, service, _) = CreateContext();
        var link = context.TrackActiveIncident(feedback);
        if (merged) link.Incident.MergedIntoIncidentId = Guid.NewGuid();
        else link.LinkRole = IncidentLinkRole.Corroborating;

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateByStaffAsync(
            context.ManagerUserId, feedback.FeedbackId, new StaffFeedbackUpdateRequest { Priority = "High" }));

        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task Update_LinkedPrimaryRemainsEditable()
    {
        var (context, feedback, service, _) = CreateContext(FeedbackStatus.Verified);
        var link = context.TrackActiveIncident(feedback);

        var result = await service.UpdateByStaffAsync(context.ManagerUserId, feedback.FeedbackId,
            new StaffFeedbackUpdateRequest { Priority = "High" });

        Assert.Equal("High", result.Priority);
        Assert.Equal(link.IncidentId, result.IncidentId);
        Assert.Equal(FeedbackStatus.Verified, result.Status);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task Update_CannotVerifyThroughStatusField()
    {
        var (context, feedback, service, _) = CreateContext();

        await Assert.ThrowsAsync<Exception>(() => service.UpdateByStaffAsync(
            context.ManagerUserId, feedback.FeedbackId,
            new StaffFeedbackUpdateRequest { Status = FeedbackStatus.Verified }));

        Assert.Equal(FeedbackStatus.AiReviewed, feedback.Status);
        Assert.Empty(context.Incidents);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    private static (DuplicateTestContext Context, Feedback Feedback, FeedbackService Service,
        IIncidentService IncidentService) CreateContext(string status = FeedbackStatus.AiReviewed)
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(Guid.NewGuid(), DateTime.UtcNow,
            isMaster: false, status: status, latitude: null, longitude: null);
        feedback.Area = context.ManagerAreaAssignments[0].Area;
        feedback.Priority = "Low";
        feedback.Severity = IncidentSeverity.Medium;
        context.Feedbacks.Add(feedback);

        var areas = Substitute.For<IGenericRepository<OperatingArea>>();
        areas.Entities.Returns(context.ManagerAreaAssignments.Select(item => item.Area).AsAsyncQueryable());
        context.UnitOfWork.GetRepository<OperatingArea>().Returns(areas);
        var categories = Substitute.For<IGenericRepository<UrbanServiceCategory>>();
        categories.Entities.Returns(new[]
        {
            new UrbanServiceCategory { CategoryId = 10, CategoryName = "Road", IsActive = true }
        }.AsAsyncQueryable());
        context.UnitOfWork.GetRepository<UrbanServiceCategory>().Returns(categories);
        var incidentService = Substitute.For<IIncidentService>();
        var service = new FeedbackService(context.UnitOfWork,
            Substitute.For<INotificationService>(), Substitute.For<IAiFeedbackReviewQueue>(),
            Substitute.For<IAiFeedbackDuplicateService>(), incidentService);
        return (context, feedback, service, incidentService);
    }
}
