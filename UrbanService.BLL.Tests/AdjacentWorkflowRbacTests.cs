using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs.SLA;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class AdjacentWorkflowRbacTests
{
    [Fact]
    public async Task InteractionMessages_StaffCannotReadUnassignedFeedback()
    {
        var context = new DuplicateTestContext();
        var staff = context.AddActor(UserRole.SYSTEMSTAFF, "Unassigned staff");
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.Assigned);
        context.Feedbacks.Add(feedback);
        context.TrackActiveIncident(feedback, incidentStatus: IncidentStatus.Assigned);
        var service = new InteractionMessageService(context.UnitOfWork);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.GetTicketMessagesAsync(staff.UserId, feedback.FeedbackId));
    }

    [Fact]
    public async Task SlaMarkResponded_RequiresAssignedIncidentToBeInProgress()
    {
        var context = new DuplicateTestContext();
        var staff = context.AddActor(UserRole.SYSTEMSTAFF, "Assigned staff");
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.Assigned);
        context.Feedbacks.Add(feedback);
        context.TrackActiveIncident(
            feedback,
            assignedStaffUserId: staff.UserId,
            incidentStatus: IncidentStatus.Assigned);
        var service = CreateSlaService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkRespondedAsync(feedback.FeedbackId, staff.UserId, null));
    }

    [Fact]
    public async Task SlaComplete_RequiresIncidentToBeApproved()
    {
        var context = new DuplicateTestContext();
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.SubmittedForApproval);
        context.Feedbacks.Add(feedback);
        context.TrackActiveIncident(
            feedback,
            incidentStatus: IncidentStatus.SubmittedForApproval);
        var service = CreateSlaService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteAsync(
                feedback.FeedbackId,
                context.ManagerUserId,
                new CompleteSlaRequest()));
    }

    [Fact]
    public async Task SlaDashboard_MixedCaseStaffRole_OnlySeesAssignedIncidents()
    {
        var context = new DuplicateTestContext();
        var staff = context.AddActor("SystemStaff", "Assigned staff");
        var assignedFeedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.InProgress);
        var unassignedFeedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.InProgress);
        context.Feedbacks.AddRange([assignedFeedback, unassignedFeedback]);
        context.TrackActiveIncident(
            assignedFeedback,
            assignedStaffUserId: staff.UserId,
            incidentStatus: IncidentStatus.InProgress);
        context.TrackActiveIncident(
            unassignedFeedback,
            incidentStatus: IncidentStatus.InProgress);

        var slas = new List<FeedbackSla>
        {
            CreateSla(assignedFeedback),
            CreateSla(unassignedFeedback)
        };
        var slaRepository = Substitute.For<IGenericRepository<FeedbackSla>>();
        slaRepository.Entities.Returns(_ => slas.AsAsyncQueryable());
        var eventRepository = Substitute.For<IGenericRepository<SlaEvent>>();
        eventRepository.Entities.Returns(_ => Array.Empty<SlaEvent>().AsAsyncQueryable());
        context.UnitOfWork.GetRepository<FeedbackSla>().Returns(slaRepository);
        context.UnitOfWork.GetRepository<SlaEvent>().Returns(eventRepository);
        var service = new SlaDashboardService(
            context.UnitOfWork,
            Microsoft.Extensions.Options.Options.Create(new SlaMonitoringOptions()));

        var result = await service.GetOverviewAsync(staff.UserId);

        Assert.Equal(1, result.TotalSla);
        Assert.Equal(1, result.RunningSla);
    }

    [Fact]
    public async Task SlaTimeline_MixedCaseServiceUserRole_AllowsOwnerAccess()
    {
        var context = new DuplicateTestContext();
        var serviceUser = context.AddActor("ServiceUser", "Resident");
        var feedback = DuplicateTestContext.Feedback(
            Guid.NewGuid(),
            DateTime.UtcNow,
            isMaster: true,
            status: FeedbackStatus.InProgress);
        feedback.UserId = serviceUser.UserId;
        context.Feedbacks.Add(feedback);

        var sla = CreateSla(feedback);
        var slaRepository = Substitute.For<IGenericRepository<FeedbackSla>>();
        slaRepository.Entities.Returns(_ => new[] { sla }.AsAsyncQueryable());
        var eventRepository = Substitute.For<IGenericRepository<SlaEvent>>();
        eventRepository.Entities.Returns(_ => Array.Empty<SlaEvent>().AsAsyncQueryable());
        context.UnitOfWork.GetRepository<FeedbackSla>().Returns(slaRepository);
        context.UnitOfWork.GetRepository<SlaEvent>().Returns(eventRepository);

        var result = await CreateSlaService(context).GetTimelineAsync(
            feedback.FeedbackId,
            serviceUser.UserId);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(nameof(SlaController.Start), UserRole.INTERACTIONMANAGER)]
    [InlineData(nameof(SlaController.Pause), UserRole.INTERACTIONMANAGER)]
    [InlineData(nameof(SlaController.Resume), UserRole.INTERACTIONMANAGER)]
    [InlineData(nameof(SlaController.Complete), UserRole.INTERACTIONMANAGER)]
    [InlineData(nameof(SlaController.Recalculate), UserRole.INTERACTIONMANAGER)]
    [InlineData(nameof(SlaController.Cancel), UserRole.INTERACTIONMANAGER)]
    [InlineData(nameof(SlaController.CheckViolation), UserRole.INTERACTIONMANAGER)]
    [InlineData(nameof(SlaController.MarkResponded), UserRole.SYSTEMSTAFF)]
    public void SlaWorkflowEndpoints_UseExpectedRole(string methodName, string expectedRole)
    {
        Assert.Equal(expectedRole, GetMethodRoles<SlaController>(methodName));
    }

    [Fact]
    public void SlaDashboard_RequiresManagementRole()
    {
        var roles = typeof(SlaDashboardController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;

        Assert.Equal(
            UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER,
            roles);
    }

    [Fact]
    public void ManualAreaAlert_IsManagerOnly()
    {
        var roles = typeof(ManagementAreaAlertsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;

        Assert.Equal(UserRole.INTERACTIONMANAGER, roles);
    }

    [Fact]
    public void SystemInteractionMessage_IsManagerOnly()
    {
        Assert.Equal(
            UserRole.INTERACTIONMANAGER,
            GetMethodRoles<InteractionMessagesController>(
                nameof(InteractionMessagesController.AddSystemMessage)));
    }

    [Theory]
    [InlineData(nameof(ManagementProviderContractsController.CreateContract))]
    [InlineData(nameof(ManagementProviderContractsController.UpdateContract))]
    [InlineData(nameof(ManagementProviderContractsController.UploadAttachments))]
    public void ProviderContractMutations_AreAdminOnly(string methodName)
    {
        Assert.Equal(
            UserRole.SYSTEMADMIN,
            GetMethodRoles<ManagementProviderContractsController>(methodName));
    }

    private static SlaService CreateSlaService(DuplicateTestContext context)
    {
        return new SlaService(
            context.UnitOfWork,
            Substitute.For<INotificationService>(),
            Substitute.For<IEmailSender>(),
            Substitute.For<ILogger<SlaService>>(),
            Microsoft.Extensions.Options.Options.Create(new SlaMonitoringOptions()),
            Substitute.For<ISlaRealtimeSender>());
    }

    private static FeedbackSla CreateSla(Feedback feedback)
    {
        return new FeedbackSla
        {
            FeedbackSlaId = Random.Shared.NextInt64(1, long.MaxValue),
            FeedbackId = feedback.FeedbackId,
            Feedback = feedback,
            Status = SlaStatus.Running,
            ResponseStatus = SlaTargetStatus.Pending,
            ResolutionStatus = SlaTargetStatus.Pending,
            IsCurrent = true,
            StartedAt = DateTime.UtcNow,
            ResponseDueAt = DateTime.UtcNow.AddHours(1),
            ResolutionDueAt = DateTime.UtcNow.AddHours(4),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string? GetMethodRoles<TController>(string methodName)
    {
        return typeof(TController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles;
    }
}
