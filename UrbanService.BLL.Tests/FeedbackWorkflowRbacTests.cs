using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class FeedbackWorkflowRbacTests
{
    [Fact]
    public async Task GetAllFeedbacks_StaffSeesOnlyFeedbacksFromAssignedIncidents()
    {
        var context = new FeedbackWorkflowRbacContext();
        var staffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var otherStaffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var assigned = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: staffUserId,
            linkRole: IncidentLinkRole.Primary);
        context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: otherStaffUserId,
            linkRole: IncidentLinkRole.Primary);
        context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: null,
            linkRole: IncidentLinkRole.Primary);

        var result = await context.Service.GetAllFeedbacksAsync(
            staffUserId,
            new FeedbackQueryParameters { PageNumber = 1, PageSize = 20 });

        var visibleFeedback = Assert.Single(result.Items);
        Assert.Equal(assigned.Feedback.FeedbackId, visibleFeedback.FeedbackId);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task EnsureManagementFeedbackReadAccess_StaffCannotReadUnassignedFeedback()
    {
        var context = new FeedbackWorkflowRbacContext();
        var staffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var otherStaffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var unassigned = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: otherStaffUserId,
            linkRole: IncidentLinkRole.Primary);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            context.Service.EnsureManagementFeedbackReadAccessAsync(
                unassigned.Feedback.FeedbackId,
                staffUserId));
    }

    [Fact]
    public async Task EnsureProviderAssignmentOperationAccess_AllowsAssignedStaffOnIncident()
    {
        var context = new FeedbackWorkflowRbacContext();
        var staffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var assigned = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: staffUserId,
            linkRole: IncidentLinkRole.Primary);
        var providerReport = context.AddProviderReport(assigned.Incident, staffUserId);

        await context.Service.EnsureProviderAssignmentOperationAccessAsync(
            providerReport.ProviderReportId,
            staffUserId);
    }

    [Fact]
    public async Task EnsureProviderAssignmentOperationAccess_RejectsStaffNotAssignedToIncident()
    {
        var context = new FeedbackWorkflowRbacContext();
        var staffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var otherStaffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var assignedToOtherStaff = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: otherStaffUserId,
            linkRole: IncidentLinkRole.Primary);
        var providerReport = context.AddProviderReport(
            assignedToOtherStaff.Incident,
            otherStaffUserId);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            context.Service.EnsureProviderAssignmentOperationAccessAsync(
                providerReport.ProviderReportId,
                staffUserId));
    }

    [Fact]
    public async Task EnsureProviderAssignmentOperationAccess_DoesNotDependOnPrimaryFeedback()
    {
        var context = new FeedbackWorkflowRbacContext();
        var staffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var secondary = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: staffUserId,
            linkRole: IncidentLinkRole.Corroborating);
        var providerReport = context.AddProviderReport(secondary.Incident, staffUserId);

        await context.Service.EnsureProviderAssignmentOperationAccessAsync(
            providerReport.ProviderReportId,
            staffUserId);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("rework")]
    public async Task ReviewResolution_ManagerOutsideIncidentWardIsRejected(string operation)
    {
        var context = new FeedbackWorkflowRbacContext();
        var managerUserId = context.AddManager(areaId: 1);
        var outsideWard = context.AddFeedbackIncident(
            areaId: 2,
            assignedStaffUserId: context.AddActor(UserRole.SYSTEMSTAFF),
            linkRole: IncidentLinkRole.Primary,
            status: FeedbackStatus.SubmittedForApproval);

        Task action = operation == "approve"
            ? context.Service.ApproveResolutionAsync(
                outsideWard.Feedback.FeedbackId,
                managerUserId,
                "Approved")
            : context.Service.RequireReworkAsync(
                outsideWard.Feedback.FeedbackId,
                managerUserId,
                "Can bo sung minh chung");

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => action);
        Assert.Empty(context.IncidentService.ReceivedCalls());
    }

    [Fact]
    public async Task RequireIncidentResolutionRework_StoresReviewMetadataAndReopensProviderAssignment()
    {
        var context = new FeedbackWorkflowRbacContext();
        var managerUserId = context.AddManager(areaId: 1);
        var assigned = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: context.AddActor(UserRole.SYSTEMSTAFF),
            linkRole: IncidentLinkRole.Primary,
            status: FeedbackStatus.SubmittedForApproval);
        var providerReport = context.AddProviderReport(assigned.Incident, assigned.Incident.AssignedStaffUserId!.Value);
        providerReport.ReportStatus = "Done";
        var resolution = context.AddResolution(assigned.Incident, providerReport);

        var result = await context.Service.RequireIncidentResolutionReworkAsync(
            assigned.Incident.IncidentId,
            resolution.ResolutionId,
            managerUserId,
            "Ảnh sau xử lý chưa thể hiện đầy đủ khu vực.");

        Assert.Equal(FeedbackStatus.NeedRework, result.Status);
        Assert.Equal(IncidentStatus.NeedRework, result.IncidentStatus);
        Assert.Equal("Ảnh sau xử lý chưa thể hiện đầy đủ khu vực.", result.ReviewReason);
        Assert.Equal(managerUserId, result.ReviewedBy?.UserId);
        Assert.NotNull(result.ReviewedAt);
        Assert.Equal("InProgress", providerReport.ReportStatus);
    }

    [Fact]
    public async Task ApproveIncidentResolution_RejectsResolutionFromStaleClient()
    {
        var context = new FeedbackWorkflowRbacContext();
        var managerUserId = context.AddManager(areaId: 1);
        var assigned = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: context.AddActor(UserRole.SYSTEMSTAFF),
            linkRole: IncidentLinkRole.Primary,
            status: FeedbackStatus.SubmittedForApproval);
        var providerReport = context.AddProviderReport(assigned.Incident, assigned.Incident.AssignedStaffUserId!.Value);
        var resolution = context.AddResolution(assigned.Incident, providerReport);

        await Assert.ThrowsAsync<ConflictException>(() =>
            context.Service.ApproveIncidentResolutionAsync(
                assigned.Incident.IncidentId,
                resolution.ResolutionId + 1,
                managerUserId,
                note: null));

        Assert.Empty(context.IncidentService.ReceivedCalls());
    }

    [Fact]
    public async Task ApproveIncidentResolution_ApprovesResolutionAndAllLinkedFeedbacks()
    {
        var context = new FeedbackWorkflowRbacContext();
        var managerUserId = context.AddManager(areaId: 1);
        var assigned = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: context.AddActor(UserRole.SYSTEMSTAFF),
            linkRole: IncidentLinkRole.Primary,
            status: FeedbackStatus.SubmittedForApproval);
        var corroboratingFeedback = context.AddFeedbackToIncident(
            assigned.Incident,
            IncidentLinkRole.Corroborating,
            FeedbackStatus.SubmittedForApproval);
        var providerReport = context.AddProviderReport(assigned.Incident, assigned.Incident.AssignedStaffUserId!.Value);
        var resolution = context.AddResolution(assigned.Incident, providerReport);

        var result = await context.Service.ApproveIncidentResolutionAsync(
            assigned.Incident.IncidentId,
            resolution.ResolutionId,
            managerUserId,
            "Đủ điều kiện phê duyệt");

        Assert.Equal(FeedbackStatus.Approved, result.Status);
        Assert.Equal(IncidentStatus.Approved, result.IncidentStatus);
        Assert.Equal(managerUserId, result.ReviewedBy?.UserId);
        Assert.Null(result.ReviewReason);
        Assert.Equal(managerUserId, assigned.Feedback.ApprovedByManagerId);
        Assert.Equal(managerUserId, corroboratingFeedback.ApprovedByManagerId);
    }

    [Fact]
    public async Task ResubmitIncidentResolution_ClearsPreviousReviewAndReturnsSubmittedStatus()
    {
        var context = new FeedbackWorkflowRbacContext();
        var staffUserId = context.AddActor(UserRole.SYSTEMSTAFF);
        var managerUserId = context.AddManager(areaId: 1);
        var assigned = context.AddFeedbackIncident(
            areaId: 1,
            assignedStaffUserId: staffUserId,
            linkRole: IncidentLinkRole.Primary,
            status: FeedbackStatus.NeedRework);
        var providerReport = context.AddProviderReport(assigned.Incident, staffUserId);
        providerReport.ReportStatus = "InProgress";
        var resolution = context.AddResolution(assigned.Incident, providerReport);
        resolution.Status = FeedbackStatus.NeedRework;
        resolution.ReviewReason = "Bổ sung ảnh";
        resolution.ReviewedByManagerId = managerUserId;
        resolution.ReviewedByManager = context.GetUser(managerUserId);
        resolution.ReviewedAt = DateTime.UtcNow;

        var result = await context.Service.SubmitIncidentResolutionAsync(
            assigned.Incident.IncidentId,
            staffUserId,
            new SubmitResolutionRequest
            {
                ProviderAssignmentId = providerReport.ProviderReportId,
                ResolutionSummary = "Đã bổ sung",
                ActionTaken = "Chụp lại ảnh hiện trường"
            });

        Assert.Equal(FeedbackStatus.SubmittedForApproval, result.Status);
        Assert.Equal(IncidentStatus.SubmittedForApproval, result.IncidentStatus);
        Assert.Null(result.ReviewReason);
        Assert.Null(result.ReviewedBy);
        Assert.Null(result.ReviewedAt);
    }

    [Theory]
    [InlineData(nameof(ManagementFeedbacksController.UpdateFeedback))]
    [InlineData(nameof(ManagementFeedbacksController.UpdateStatus))]
    [InlineData(nameof(ManagementFeedbacksController.VerifyFeedback))]
    [InlineData(nameof(ManagementFeedbacksController.ApproveResolution))]
    [InlineData(nameof(ManagementFeedbacksController.NeedRework))]
    public void ManagerWorkflowActions_RequireManagerRole(string methodName)
    {
        AssertActionRole(
            typeof(ManagementFeedbacksController),
            methodName,
            UserRole.INTERACTIONMANAGER);
    }

    [Theory]
    [InlineData(nameof(ManagementIncidentsController.ApproveResolution))]
    [InlineData(nameof(ManagementIncidentsController.NeedRework))]
    public void IncidentResolutionReviewActions_RequireManagerRole(string methodName)
    {
        AssertActionRole(
            typeof(ManagementIncidentsController),
            methodName,
            UserRole.INTERACTIONMANAGER);
    }

    [Theory]
    [InlineData(typeof(ManagementIncidentsController), nameof(ManagementIncidentsController.GetProviderCandidates))]
    [InlineData(typeof(ManagementFeedbacksController), nameof(ManagementFeedbacksController.NotifyProviderResult))]
    [InlineData(typeof(ManagementIncidentsController), nameof(ManagementIncidentsController.AssignProvider))]
    [InlineData(typeof(ManagementIncidentsController), nameof(ManagementIncidentsController.SubmitResolution))]
    [InlineData(typeof(ManagementProviderReportsController), nameof(ManagementProviderReportsController.UpdateStatus))]
    [InlineData(typeof(ManagementProviderReportsController), nameof(ManagementProviderReportsController.AddContactLog))]
    [InlineData(typeof(ManagementProviderReportsController), nameof(ManagementProviderReportsController.AddCompletionDocuments))]
    [InlineData(typeof(ManagementProviderReportsController), nameof(ManagementProviderReportsController.ClearCompletionDocuments))]
    public void ProviderWorkflowActions_RequireStaffRole(Type controllerType, string methodName)
    {
        AssertActionRole(controllerType, methodName, UserRole.SYSTEMSTAFF);
    }

    private static void AssertActionRole(
        Type controllerType,
        string methodName,
        string expectedRole)
    {
        var action = controllerType.GetMethod(methodName)!;
        var authorize = Assert.Single(action
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(expectedRole, authorize.Roles);
    }

    private sealed class FeedbackWorkflowRbacContext
    {
        private readonly List<User> _users = [];
        private readonly List<Feedback> _feedbacks = [];
        private readonly List<Incident> _incidents = [];
        private readonly List<IncidentReportLink> _incidentReportLinks = [];
        private readonly List<ManagerAreaAssignment> _managerAreaAssignments = [];
        private readonly List<FeedbackProviderReport> _providerReports = [];
        private readonly List<FeedbackResolution> _resolutions = [];
        private int _nextRoleId = 1;
        private int _nextProviderReportId = 1;

        public FeedbackWorkflowRbacContext()
        {
            ConfigureRepository(_users);
            ConfigureRepository(_feedbacks);
            ConfigureRepository(_incidents);
            ConfigureRepository(_incidentReportLinks);
            ConfigureRepository(_managerAreaAssignments);
            ConfigureRepository(_providerReports);
            ConfigureRepository(_resolutions);

            Service = new FeedbackService(
                UnitOfWork,
                Substitute.For<INotificationService>(),
                Substitute.For<IAiFeedbackReviewQueue>(),
                Substitute.For<IAiFeedbackDuplicateService>(),
                    IncidentService);

            IncidentService.UpdateStatusAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<UpdateIncidentStatusRequest>(),
                    Arg.Any<Guid>(),
                    Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var incidentId = call.ArgAt<Guid>(0);
                    var request = call.ArgAt<UpdateIncidentStatusRequest>(1);
                    var incident = _incidents.Single(item => item.IncidentId == incidentId);
                    incident.Status = request.Status;
                    return new IncidentDetailDto
                    {
                        IncidentId = incidentId,
                        Status = request.Status,
                        Title = incident.Title,
                        LocationText = incident.LocationText,
                        Severity = incident.Severity,
                        CreatedAt = incident.CreatedAt
                    };
                });

            IncidentService.UpdateStatusFromProviderAssignmentAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<UpdateIncidentStatusRequest>(),
                    Arg.Any<Guid>(),
                    Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var incident = _incidents.Single(item => item.IncidentId == call.ArgAt<Guid>(0));
                    incident.Status = call.ArgAt<UpdateIncidentStatusRequest>(1).Status;
                    return Task.CompletedTask;
                });
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IIncidentService IncidentService { get; } = Substitute.For<IIncidentService>();

        public FeedbackService Service { get; }

        public Guid AddActor(string roleName)
        {
            var role = new Role
            {
                RoleId = _nextRoleId++,
                RoleName = roleName
            };
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = role.RoleId,
                Role = role,
                FullName = $"{roleName} test user",
                Email = $"{Guid.NewGuid():N}@example.test",
                PasswordHash = "test",
                IsActive = true,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            _users.Add(user);
            return user.UserId;
        }

        public Guid AddManager(int areaId)
        {
            var managerUserId = AddActor(UserRole.INTERACTIONMANAGER);
            var area = CreateArea(areaId);
            _managerAreaAssignments.Add(new ManagerAreaAssignment
            {
                ManagerAreaAssignmentId = _managerAreaAssignments.Count + 1,
                ManagerUserId = managerUserId,
                AreaId = area.AreaId,
                Area = area,
                CreatedByUserId = managerUserId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            return managerUserId;
        }

        public User GetUser(Guid userId) => _users.Single(user => user.UserId == userId);

        public (Feedback Feedback, Incident Incident) AddFeedbackIncident(
            int areaId,
            Guid? assignedStaffUserId,
            string linkRole,
            string status = FeedbackStatus.Assigned)
        {
            var area = CreateArea(areaId);
            var category = new UrbanServiceCategory
            {
                CategoryId = 1,
                CategoryName = "Road",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var residentUserId = AddActor(UserRole.SERVICEUSER);
            var resident = _users.Single(user => user.UserId == residentUserId);
            var feedback = new Feedback
            {
                FeedbackId = Guid.NewGuid(),
                UserId = residentUserId,
                User = resident,
                AreaId = areaId,
                Area = area,
                CategoryId = category.CategoryId,
                Category = category,
                Title = $"Feedback in area {areaId}",
                Description = "Test feedback",
                LocationText = $"Ward {areaId}",
                SubmissionChannel = FeedbackSubmissionChannel.Web,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            var incident = new Incident
            {
                IncidentId = Guid.NewGuid(),
                AreaId = areaId,
                Area = area,
                CategoryId = category.CategoryId,
                Category = category,
                Title = feedback.Title,
                LocationText = feedback.LocationText,
                Severity = "Normal",
                Status = status,
                AssignedStaffUserId = assignedStaffUserId,
                CreatedAt = feedback.CreatedAt
            };
            var link = new IncidentReportLink
            {
                IncidentReportLinkId = Guid.NewGuid(),
                IncidentId = incident.IncidentId,
                Incident = incident,
                FeedbackId = feedback.FeedbackId,
                Feedback = feedback,
                LinkStatus = IncidentLinkStatus.Active,
                LinkMethod = IncidentLinkMethod.Created,
                LinkRole = linkRole,
                LinkedAt = feedback.CreatedAt
            };

            feedback.IncidentReportLinks.Add(link);
            incident.IncidentReportLinks.Add(link);
            _feedbacks.Add(feedback);
            _incidents.Add(incident);
            _incidentReportLinks.Add(link);
            return (feedback, incident);
        }

        public FeedbackProviderReport AddProviderReport(Incident incident, Guid reportedByUserId)
        {
            var report = new FeedbackProviderReport
            {
                ProviderReportId = _nextProviderReportId++,
                IncidentId = incident.IncidentId,
                Incident = incident,
                CoordinatorId = 1,
                ReportedByUserId = reportedByUserId,
                ReportStatus = "Reported",
                ReportedAt = DateTime.UtcNow
            };
            incident.ProviderAssignments.Add(report);
            _providerReports.Add(report);
            return report;
        }

        public Feedback AddFeedbackToIncident(
            Incident incident,
            string linkRole,
            string status)
        {
            var area = incident.Area;
            var category = incident.Category;
            var residentUserId = AddActor(UserRole.SERVICEUSER);
            var feedback = new Feedback
            {
                FeedbackId = Guid.NewGuid(),
                UserId = residentUserId,
                User = GetUser(residentUserId),
                AreaId = incident.AreaId,
                Area = area,
                CategoryId = incident.CategoryId,
                Category = category,
                Title = "Corroborating feedback",
                Description = "Additional report",
                LocationText = incident.LocationText,
                SubmissionChannel = FeedbackSubmissionChannel.Web,
                Status = status,
                CreatedAt = DateTime.UtcNow
            };
            var link = new IncidentReportLink
            {
                IncidentReportLinkId = Guid.NewGuid(),
                IncidentId = incident.IncidentId,
                Incident = incident,
                FeedbackId = feedback.FeedbackId,
                Feedback = feedback,
                LinkStatus = IncidentLinkStatus.Active,
                LinkMethod = IncidentLinkMethod.StaffConfirmed,
                LinkRole = linkRole,
                LinkedAt = DateTime.UtcNow
            };
            feedback.IncidentReportLinks.Add(link);
            incident.IncidentReportLinks.Add(link);
            _feedbacks.Add(feedback);
            _incidentReportLinks.Add(link);
            return feedback;
        }

        public FeedbackResolution AddResolution(
            Incident incident,
            FeedbackProviderReport providerReport)
        {
            var staff = _users.Single(user => user.UserId == providerReport.ReportedByUserId);
            var resolution = new FeedbackResolution
            {
                ResolutionId = _resolutions.Count + 1,
                IncidentId = incident.IncidentId,
                Incident = incident,
                ProviderReportId = providerReport.ProviderReportId,
                ProviderReport = providerReport,
                CreatedByStaffUserId = staff.UserId,
                CreatedByStaffUser = staff,
                ResolutionSummary = "Đã xử lý",
                ActionTaken = "Khắc phục hiện trường",
                ResolvedAt = DateTime.UtcNow,
                Status = FeedbackStatus.SubmittedForApproval
            };
            incident.Resolutions.Add(resolution);
            providerReport.FeedbackResolutions.Add(resolution);
            _resolutions.Add(resolution);
            return resolution;
        }

        private void ConfigureRepository<T>(List<T> entities)
            where T : class
        {
            var repository = Substitute.For<IGenericRepository<T>>();
            repository.Entities.Returns(_ => entities.AsAsyncQueryable());
            UnitOfWork.GetRepository<T>().Returns(repository);
        }

        private static OperatingArea CreateArea(int areaId)
        {
            return new OperatingArea
            {
                AreaId = areaId,
                AreaName = $"Ward {areaId}",
                AreaType = "Ward",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
