using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public sealed class IncidentServiceTests
{
    [Fact]
    public async Task StageNewReport_CreatesIncidentLinkSubscriptionAndEvents()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);

        var incidentId = await service.StageNewReportIncidentAsync(feedback, feedback.UserId, now);

        var incident = Assert.Single(context.Incidents);
        Assert.Equal(incidentId, incident.IncidentId);
        Assert.Equal(feedback.AreaId, incident.AreaId);
        Assert.Equal(IncidentStatus.New, incident.Status);
        Assert.Equal(IncidentSeverity.Medium, incident.Severity);

        var link = Assert.Single(context.Links);
        Assert.Equal(incidentId, link.IncidentId);
        Assert.Equal(feedback.FeedbackId, link.FeedbackId);
        Assert.Equal(IncidentLinkStatus.Active, link.LinkStatus);
        Assert.Equal(IncidentLinkMethod.Created, link.LinkMethod);
        Assert.Equal(IncidentLinkRole.Primary, link.LinkRole);

        var subscription = Assert.Single(context.Subscriptions);
        Assert.Equal(incidentId, subscription.IncidentId);
        Assert.Equal(feedback.UserId, subscription.UserId);
        Assert.True(subscription.IsActive);

        Assert.Equal(2, context.Events.Count);
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.IncidentCreated);
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.ReportLinked);
    }

    [Fact]
    public async Task RelinkConfirmedDuplicate_MovesChildAndMergesEmptyIncident()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var createdAt = DateTime.UtcNow;
        var parent = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), createdAt.AddMinutes(-10));
        var child = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), createdAt);
        parent.Status = FeedbackStatus.InProgress;
        child.Status = FeedbackStatus.AiReviewed;
        var targetIncident = IncidentTestContext.Incident(Guid.NewGuid(), parent, createdAt.AddMinutes(-10));
        var childIncident = IncidentTestContext.Incident(Guid.NewGuid(), child, createdAt);
        context.Incidents.AddRange([targetIncident, childIncident]);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(targetIncident, parent, IncidentLinkRole.Primary, createdAt.AddMinutes(-10)),
            IncidentTestContext.Link(childIncident, child, IncidentLinkRole.Primary, createdAt)
        ]);
        context.Subscriptions.Add(new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = childIncident.IncidentId,
            UserId = child.UserId,
            SourceType = IncidentSubscriptionSource.Report,
            SourceFeedbackId = child.FeedbackId,
            IsActive = true,
            CreatedAt = createdAt
        });

        var result = await service.RelinkConfirmedDuplicateAsync(
            child,
            parent,
            Guid.NewGuid(),
            0.94m,
            "Cùng sự vụ và vị trí");

        Assert.Equal(targetIncident.IncidentId, result);
        var oldChildLink = context.Links.Single(link =>
            link.FeedbackId == child.FeedbackId && link.IncidentId == childIncident.IncidentId);
        Assert.Equal(IncidentLinkStatus.Unlinked, oldChildLink.LinkStatus);
        Assert.NotNull(oldChildLink.UnlinkedAt);

        var activeChildLink = context.Links.Single(link =>
            link.FeedbackId == child.FeedbackId && link.LinkStatus == IncidentLinkStatus.Active);
        Assert.Equal(targetIncident.IncidentId, activeChildLink.IncidentId);
        Assert.Equal(IncidentLinkMethod.StaffConfirmed, activeChildLink.LinkMethod);
        Assert.Equal(IncidentLinkRole.Corroborating, activeChildLink.LinkRole);
        Assert.Equal(0.94m, activeChildLink.ConfidenceScore);

        Assert.Equal("Merged", childIncident.Status);
        Assert.Equal(targetIncident.IncidentId, childIncident.MergedIntoIncidentId);
        Assert.False(context.Subscriptions.Single(item =>
            item.IncidentId == childIncident.IncidentId && item.UserId == child.UserId).IsActive);
        Assert.Contains(context.Subscriptions, item =>
            item.IncidentId == targetIncident.IncidentId &&
            item.UserId == child.UserId &&
            item.IsActive);
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.IncidentMerged);
        Assert.Equal(FeedbackStatus.InProgress, child.Status);
        Assert.Contains(context.StatusHistories, history =>
            history.FeedbackId == child.FeedbackId &&
            history.OldStatus == FeedbackStatus.AiReviewed &&
            history.NewStatus == FeedbackStatus.InProgress);
    }

    [Fact]
    public async Task RelinkConfirmedDuplicate_WhenAlreadyLinked_RepairsStatusProjection()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var parent = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var child = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        child.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), parent, now.AddMinutes(-5));
        incident.Status = IncidentStatus.InProgress;
        context.Incidents.Add(incident);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(incident, parent, IncidentLinkRole.Primary, now.AddMinutes(-5)),
            IncidentTestContext.Link(incident, child, IncidentLinkRole.Corroborating, now)
        ]);

        var result = await service.RelinkConfirmedDuplicateAsync(
            child,
            parent,
            Guid.NewGuid(),
            0.94m,
            "Retry duplicate confirmation");

        Assert.Equal(incident.IncidentId, result);
        Assert.Equal(2, context.Links.Count);
        Assert.Equal(FeedbackStatus.InProgress, child.Status);
        Assert.Contains(context.StatusHistories, history =>
            history.FeedbackId == child.FeedbackId &&
            history.OldStatus == FeedbackStatus.AiReviewed &&
            history.NewStatus == FeedbackStatus.InProgress);
    }

    [Fact]
    public async Task UpdateStatus_ProjectsStatusToAllActiveReports()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var primary = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var corroborating = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        primary.Status = FeedbackStatus.Verified;
        corroborating.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), primary, now.AddMinutes(-5));
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.AddRange([primary, corroborating]);
        context.Links.AddRange(
        [
            IncidentTestContext.Link(incident, primary, IncidentLinkRole.Primary, now.AddMinutes(-5)),
            IncidentTestContext.Link(incident, corroborating, IncidentLinkRole.Corroborating, now)
        ]);
        var actorUserId = Guid.NewGuid();

        var result = await service.UpdateStatusAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.Assigned,
                Note = "Assigned to provider"
            },
            actorUserId);

        Assert.Equal(IncidentStatus.Assigned, result.Status);
        Assert.Equal(FeedbackStatus.Assigned, primary.Status);
        Assert.Equal(FeedbackStatus.Assigned, corroborating.Status);
        Assert.Equal(2, context.StatusHistories.Count);
        Assert.All(context.StatusHistories, history =>
        {
            Assert.Equal(FeedbackStatus.Assigned, history.NewStatus);
            Assert.Equal(actorUserId, history.ChangedByUserId);
            Assert.Equal("Assigned to provider", history.Note);
        });
        Assert.Contains(context.Events, item => item.EventType == IncidentEventType.StatusChanged);
    }

    [Fact]
    public async Task UpdateStatus_DoesNotProjectStatusToUnlinkedReport()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var active = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var unlinked = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        active.Status = FeedbackStatus.Verified;
        unlinked.Status = FeedbackStatus.AiReviewed;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), active, now.AddMinutes(-5));
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.AddRange([active, unlinked]);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            active,
            IncidentLinkRole.Primary,
            now.AddMinutes(-5)));
        var oldLink = IncidentTestContext.Link(
            incident,
            unlinked,
            IncidentLinkRole.Corroborating,
            now);
        oldLink.LinkStatus = IncidentLinkStatus.Unlinked;
        oldLink.UnlinkedAt = now;
        context.Links.Add(oldLink);

        await service.UpdateStatusAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.InProgress
            },
            Guid.NewGuid());

        Assert.Equal(FeedbackStatus.InProgress, active.Status);
        Assert.Equal(FeedbackStatus.AiReviewed, unlinked.Status);
        Assert.Single(context.StatusHistories);
        Assert.Equal(active.FeedbackId, context.StatusHistories[0].FeedbackId);
    }

    [Fact]
    public async Task UpdateStatusFromFeedback_ResolvesActiveIncidentAndReturnsLegacyHistory()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));
        var actorUserId = Guid.NewGuid();

        var result = await service.UpdateStatusFromFeedbackAsync(
            feedback.FeedbackId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.InProgress,
                Note = "Legacy route"
            },
            actorUserId);

        Assert.Equal(feedback.FeedbackId, result.FeedbackId);
        Assert.Equal(FeedbackStatus.Verified, result.OldStatus);
        Assert.Equal(FeedbackStatus.InProgress, result.NewStatus);
        Assert.Equal("Legacy route", result.Note);
        Assert.Equal(IncidentStatus.InProgress, incident.Status);
    }

    [Fact]
    public async Task UpdateStatus_RejectsRollbackToNew()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        feedback.Status = FeedbackStatus.Verified;
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.Verified;
        context.Incidents.Add(incident);
        context.Feedbacks.Add(feedback);
        context.Links.Add(IncidentTestContext.Link(
            incident,
            feedback,
            IncidentLinkRole.Primary,
            now));

        await Assert.ThrowsAsync<Exception>(() => service.UpdateStatusAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.UpdateIncidentStatusRequest
            {
                Status = IncidentStatus.New
            },
            Guid.NewGuid()));

        Assert.Equal(IncidentStatus.Verified, incident.Status);
        Assert.Equal(FeedbackStatus.Verified, feedback.Status);
        Assert.Empty(context.StatusHistories);
        context.UnitOfWork.Received(1).RollBack();
    }

    [Fact]
    public async Task GetAssigneeCandidates_ReturnsOnlyActiveStaffForIncidentScope()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        context.Incidents.Add(incident);

        var eligibleStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Eligible staff");
        var wrongAreaStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Wrong area staff");
        var wrongCategoryStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Wrong category staff");
        var inactiveStaff = IncidentTestContext.Staff(Guid.NewGuid(), "Inactive staff");
        inactiveStaff.IsActive = false;
        var otherArea = new OperatingArea
        {
            AreaId = incident.AreaId + 1,
            AreaName = "Area 2",
            AreaType = "Ward",
            IsActive = true
        };
        context.Assignments.AddRange(
        [
            IncidentTestContext.Assignment(eligibleStaff, incident.Area, incident.CategoryId),
            IncidentTestContext.Assignment(wrongAreaStaff, otherArea, incident.CategoryId),
            IncidentTestContext.Assignment(wrongCategoryStaff, incident.Area, incident.CategoryId + 1),
            IncidentTestContext.Assignment(inactiveStaff, incident.Area, incident.CategoryId)
        ]);

        var result = await service.GetAssigneeCandidatesAsync(incident.IncidentId);

        var candidate = Assert.Single(result);
        Assert.Equal(eligibleStaff.UserId, candidate.UserId);
        Assert.Equal(incident.AreaId, candidate.AreaId);
        Assert.Equal(incident.CategoryId, candidate.CategoryId);
    }

    [Fact]
    public async Task Assign_SavesStaffAndAuditEvent_WhenStaffMatchesIncidentScope()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        incident.Status = IncidentStatus.New;
        context.Incidents.Add(incident);
        var staff = IncidentTestContext.Staff(Guid.NewGuid(), "Assigned staff");
        context.Assignments.Add(IncidentTestContext.Assignment(staff, incident.Area, incident.CategoryId));
        var actorUserId = Guid.NewGuid();

        var result = await service.AssignAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.AssignIncidentRequest
            {
                StaffUserId = staff.UserId,
                Reason = "Phụ trách đúng phường và danh mục"
            },
            actorUserId);

        Assert.Equal(staff.UserId, incident.AssignedStaffUserId);
        Assert.Equal(staff.UserId, result.AssignedStaffUserId);
        Assert.Contains(context.Events, incidentEvent =>
            incidentEvent.IncidentId == incident.IncidentId &&
            incidentEvent.EventType == IncidentEventType.AssigneeChanged &&
            incidentEvent.ActorUserId == actorUserId &&
            incidentEvent.PayloadJson != null &&
            incidentEvent.PayloadJson.Contains(staff.UserId.ToString(), StringComparison.Ordinal));
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task Assign_RejectsStaffOutsideIncidentCategory()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var feedback = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), feedback, now);
        context.Incidents.Add(incident);
        var staff = IncidentTestContext.Staff(Guid.NewGuid(), "Wrong category staff");
        context.Assignments.Add(IncidentTestContext.Assignment(staff, incident.Area, incident.CategoryId + 1));

        var exception = await Assert.ThrowsAsync<Exception>(() => service.AssignAsync(
            incident.IncidentId,
            new UrbanService.BLL.Dtos.AssignIncidentRequest { StaffUserId = staff.UserId },
            Guid.NewGuid()));

        Assert.Contains("khu vực và danh mục", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(incident.AssignedStaffUserId);
        Assert.DoesNotContain(context.Events, incidentEvent =>
            incidentEvent.EventType == IncidentEventType.AssigneeChanged);
    }

    [Fact]
    public async Task UnlinkReport_PreservesHistoryAndPromotesRemainingPrimary()
    {
        var context = new IncidentTestContext();
        var service = new IncidentService(context.UnitOfWork);
        var now = DateTime.UtcNow;
        var primaryReport = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5));
        var remainingReport = IncidentTestContext.Feedback(Guid.NewGuid(), Guid.NewGuid(), now);
        var incident = IncidentTestContext.Incident(Guid.NewGuid(), primaryReport, now.AddMinutes(-5));
        context.Incidents.Add(incident);
        var primaryLink = IncidentTestContext.Link(incident, primaryReport, IncidentLinkRole.Primary, now.AddMinutes(-5));
        var remainingLink = IncidentTestContext.Link(incident, remainingReport, IncidentLinkRole.Corroborating, now);
        context.Links.AddRange([primaryLink, remainingLink]);
        context.Subscriptions.Add(new IncidentSubscription
        {
            IncidentSubscriptionId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            UserId = primaryReport.UserId,
            SourceType = IncidentSubscriptionSource.Report,
            SourceFeedbackId = primaryReport.FeedbackId,
            IsActive = true,
            CreatedAt = now.AddMinutes(-5)
        });

        await service.UnlinkReportAsync(
            incident.IncidentId,
            primaryReport.FeedbackId,
            Guid.NewGuid());

        Assert.Equal(IncidentLinkStatus.Unlinked, primaryLink.LinkStatus);
        Assert.NotNull(primaryLink.UnlinkedAt);
        Assert.Equal(IncidentLinkRole.Primary, remainingLink.LinkRole);
        Assert.False(Assert.Single(context.Subscriptions).IsActive);
        Assert.Contains(context.Events, item =>
            item.EventType == IncidentEventType.ReportUnlinked &&
            item.FeedbackId == primaryReport.FeedbackId);
        context.UnitOfWork.Received(1).CommitTransaction();
    }

    private sealed class IncidentTestContext
    {
        public IncidentTestContext()
        {
            ConfigureRepository(IncidentRepository, Incidents);
            ConfigureRepository(LinkRepository, Links);
            ConfigureRepository(SubscriptionRepository, Subscriptions);
            ConfigureRepository(EventRepository, Events);
            ConfigureRepository(FeedbackRepository, Feedbacks);
            ConfigureRepository(StatusHistoryRepository, StatusHistories);
            ConfigureRepository(AssignmentRepository, Assignments);

            UnitOfWork.GetRepository<Incident>().Returns(IncidentRepository);
            UnitOfWork.GetRepository<IncidentReportLink>().Returns(LinkRepository);
            UnitOfWork.GetRepository<IncidentSubscription>().Returns(SubscriptionRepository);
            UnitOfWork.GetRepository<IncidentEvent>().Returns(EventRepository);
            UnitOfWork.GetRepository<Feedback>().Returns(FeedbackRepository);
            UnitOfWork.GetRepository<FeedbackStatusHistory>().Returns(StatusHistoryRepository);
            UnitOfWork.GetRepository<StaffAreaAssignment>().Returns(AssignmentRepository);
            UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
            UnitOfWork.AcquireTransactionAdvisoryLockAsync(Arg.Any<long>()).Returns(Task.CompletedTask);
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public IGenericRepository<Incident> IncidentRepository { get; } = Substitute.For<IGenericRepository<Incident>>();
        public IGenericRepository<IncidentReportLink> LinkRepository { get; } = Substitute.For<IGenericRepository<IncidentReportLink>>();
        public IGenericRepository<IncidentSubscription> SubscriptionRepository { get; } = Substitute.For<IGenericRepository<IncidentSubscription>>();
        public IGenericRepository<IncidentEvent> EventRepository { get; } = Substitute.For<IGenericRepository<IncidentEvent>>();
        public IGenericRepository<Feedback> FeedbackRepository { get; } = Substitute.For<IGenericRepository<Feedback>>();
        public IGenericRepository<FeedbackStatusHistory> StatusHistoryRepository { get; } = Substitute.For<IGenericRepository<FeedbackStatusHistory>>();
        public IGenericRepository<StaffAreaAssignment> AssignmentRepository { get; } = Substitute.For<IGenericRepository<StaffAreaAssignment>>();

        public List<Incident> Incidents { get; } = [];
        public List<IncidentReportLink> Links { get; } = [];
        public List<IncidentSubscription> Subscriptions { get; } = [];
        public List<IncidentEvent> Events { get; } = [];
        public List<Feedback> Feedbacks { get; } = [];
        public List<FeedbackStatusHistory> StatusHistories { get; } = [];
        public List<StaffAreaAssignment> Assignments { get; } = [];

        public static Feedback Feedback(Guid feedbackId, Guid userId, DateTime createdAt)
        {
            return new Feedback
            {
                FeedbackId = feedbackId,
                UserId = userId,
                AreaId = 1,
                CategoryId = 1,
                Title = $"Feedback {feedbackId}",
                Description = "Mô tả sự vụ",
                LocationText = "Quận 1",
                Latitude = 10.762622m,
                Longitude = 106.660172m,
                Priority = "Medium",
                Status = "Submitted",
                SubmissionChannel = "Web",
                User = new User
                {
                    UserId = userId,
                    FullName = $"User {userId}",
                    Email = $"{userId}@example.test"
                },
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }

        public static Incident Incident(Guid incidentId, Feedback feedback, DateTime createdAt)
        {
            return new Incident
            {
                IncidentId = incidentId,
                AreaId = feedback.AreaId,
                CategoryId = feedback.CategoryId,
                Title = feedback.Title,
                Description = feedback.Description,
                LocationText = feedback.LocationText,
                Severity = IncidentSeverity.Medium,
                Status = feedback.Status,
                Area = new OperatingArea
                {
                    AreaId = feedback.AreaId,
                    AreaName = "Area 1",
                    AreaType = "Ward",
                    IsActive = true
                },
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }

        public static IncidentReportLink Link(
            Incident incident,
            Feedback feedback,
            string role,
            DateTime linkedAt)
        {
            return new IncidentReportLink
            {
                IncidentReportLinkId = Guid.NewGuid(),
                IncidentId = incident.IncidentId,
                Incident = incident,
                FeedbackId = feedback.FeedbackId,
                Feedback = feedback,
                LinkStatus = IncidentLinkStatus.Active,
                LinkMethod = IncidentLinkMethod.Created,
                LinkRole = role,
                LinkedAt = linkedAt
            };
        }

        public static User Staff(Guid userId, string fullName)
        {
            return new User
            {
                UserId = userId,
                FullName = fullName,
                Email = $"{userId}@example.test",
                IsActive = true,
                Role = new Role
                {
                    RoleId = 2,
                    RoleName = UserRole.SYSTEMSTAFF
                }
            };
        }

        public static StaffAreaAssignment Assignment(
            User staff,
            OperatingArea area,
            int? categoryId)
        {
            return new StaffAreaAssignment
            {
                StaffAreaAssignmentId = Random.Shared.Next(1, int.MaxValue),
                UserId = staff.UserId,
                User = staff,
                AreaId = area.AreaId,
                Area = area,
                CategoryId = categoryId,
                Category = categoryId.HasValue
                    ? new UrbanServiceCategory
                    {
                        CategoryId = categoryId.Value,
                        CategoryName = $"Category {categoryId.Value}",
                        IsActive = true
                    }
                    : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        private static void ConfigureRepository<T>(
            IGenericRepository<T> repository,
            List<T> entities)
            where T : class
        {
            repository.Entities.Returns(_ => entities.AsAsyncQueryable());
            repository.AddAsync(Arg.Any<T>()).Returns(call =>
            {
                entities.Add(call.Arg<T>());
                return Task.CompletedTask;
            });
            repository.AddRangeAsync(Arg.Any<IEnumerable<T>>()).Returns(call =>
            {
                entities.AddRange(call.Arg<IEnumerable<T>>());
                return Task.CompletedTask;
            });
        }
    }
}
