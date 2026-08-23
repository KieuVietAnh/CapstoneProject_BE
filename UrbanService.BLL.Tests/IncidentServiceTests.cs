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
        Assert.Equal(feedback.Status, incident.Status);

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

            UnitOfWork.GetRepository<Incident>().Returns(IncidentRepository);
            UnitOfWork.GetRepository<IncidentReportLink>().Returns(LinkRepository);
            UnitOfWork.GetRepository<IncidentSubscription>().Returns(SubscriptionRepository);
            UnitOfWork.GetRepository<IncidentEvent>().Returns(EventRepository);
            UnitOfWork.GetRepository<Feedback>().Returns(FeedbackRepository);
            UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
            UnitOfWork.AcquireTransactionAdvisoryLockAsync(Arg.Any<long>()).Returns(Task.CompletedTask);
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public IGenericRepository<Incident> IncidentRepository { get; } = Substitute.For<IGenericRepository<Incident>>();
        public IGenericRepository<IncidentReportLink> LinkRepository { get; } = Substitute.For<IGenericRepository<IncidentReportLink>>();
        public IGenericRepository<IncidentSubscription> SubscriptionRepository { get; } = Substitute.For<IGenericRepository<IncidentSubscription>>();
        public IGenericRepository<IncidentEvent> EventRepository { get; } = Substitute.For<IGenericRepository<IncidentEvent>>();
        public IGenericRepository<Feedback> FeedbackRepository { get; } = Substitute.For<IGenericRepository<Feedback>>();

        public List<Incident> Incidents { get; } = [];
        public List<IncidentReportLink> Links { get; } = [];
        public List<IncidentSubscription> Subscriptions { get; } = [];
        public List<IncidentEvent> Events { get; } = [];
        public List<Feedback> Feedbacks { get; } = [];

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
                Status = feedback.Status,
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
