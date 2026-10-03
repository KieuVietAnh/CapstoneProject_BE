using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Data;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public sealed class IncidentAdminDeleteTests
{
    [Fact]
    public async Task DeleteByManagementAsync_DeletesMergedTreeAndDependentRecords()
    {
        var context = new DeleteTestContext();
        var root = CreateIncident(Guid.NewGuid());
        var mergedChild = CreateIncident(Guid.NewGuid(), root.IncidentId);
        var mergedGrandchild = CreateIncident(Guid.NewGuid(), mergedChild.IncidentId);
        var unrelated = CreateIncident(Guid.NewGuid());
        context.Incidents.AddRange([root, mergedChild, mergedGrandchild, unrelated]);

        context.CompletionDocuments.AddRange([
            new CompletionDocument { CompletionDocumentId = 1, IncidentId = root.IncidentId },
            new CompletionDocument { CompletionDocumentId = 2, IncidentId = mergedChild.IncidentId },
            new CompletionDocument { CompletionDocumentId = 3, IncidentId = unrelated.IncidentId }
        ]);
        context.Resolutions.AddRange([
            new FeedbackResolution { ResolutionId = 1, IncidentId = mergedGrandchild.IncidentId },
            new FeedbackResolution { ResolutionId = 2, IncidentId = unrelated.IncidentId }
        ]);
        context.ProviderReports.AddRange([
            new FeedbackProviderReport { ProviderReportId = 1, IncidentId = root.IncidentId },
            new FeedbackProviderReport { ProviderReportId = 2, IncidentId = unrelated.IncidentId }
        ]);
        context.Notifications.AddRange([
            new Notification { NotificationId = 1, IncidentId = root.IncidentId },
            new Notification { NotificationId = 2, IncidentId = mergedGrandchild.IncidentId },
            new Notification { NotificationId = 3, IncidentId = unrelated.IncidentId }
        ]);

        await new IncidentService(context.UnitOfWork)
            .DeleteByManagementAsync(root.IncidentId);

        Assert.Equal([unrelated.IncidentId], context.Incidents.Select(item => item.IncidentId));
        Assert.All(context.CompletionDocuments, item => Assert.Equal(unrelated.IncidentId, item.IncidentId));
        Assert.All(context.Resolutions, item => Assert.Equal(unrelated.IncidentId, item.IncidentId));
        Assert.All(context.ProviderReports, item => Assert.Equal(unrelated.IncidentId, item.IncidentId));
        Assert.All(context.Notifications, item => Assert.Equal(unrelated.IncidentId, item.IncidentId));
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task DeleteByManagementAsync_MissingIncident_DoesNotDeleteOrSave()
    {
        var context = new DeleteTestContext();
        var service = new IncidentService(context.UnitOfWork);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.DeleteByManagementAsync(Guid.NewGuid()));

        Assert.Equal("Không tìm thấy sự vụ.", exception.Message);
        context.IncidentRepository.DidNotReceive()
            .DeleteRange(Arg.Any<IEnumerable<Incident>>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task DeleteIncident_ActionReturnsNoContentAndCallsManagementDelete()
    {
        var incidentService = Substitute.For<IIncidentService>();
        var controller = CreateController(incidentService);
        var incidentId = Guid.NewGuid();

        var result = await controller.DeleteIncident(incidentId);

        Assert.IsType<NoContentResult>(result);
        await incidentService.Received(1).DeleteByManagementAsync(
            incidentId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeleteIncident_ActionRequiresSystemAdminAndGuidRoute()
    {
        var action = typeof(ManagementIncidentsController)
            .GetMethod(nameof(ManagementIncidentsController.DeleteIncident))!;

        var authorize = Assert.Single(action
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(UserRole.SYSTEMADMIN, authorize.Roles);

        var httpDelete = Assert.Single(action
            .GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true)
            .Cast<HttpDeleteAttribute>());
        Assert.Equal("{incidentId:guid}", httpDelete.Template);

        var responseTypes = action
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: true)
            .Cast<ProducesResponseTypeAttribute>();
        Assert.Contains(responseTypes, response => response.StatusCode == StatusCodes.Status204NoContent);
    }

    [Fact]
    public void IncidentDeletionRelationships_MatchServiceDeletionPlan()
    {
        var options = new DbContextOptionsBuilder<UrbanServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=urbanservice_model_test")
            .Options;
        using var dbContext = new UrbanServiceDbContext(options);

        AssertIncidentDeleteBehavior<IncidentComment>(dbContext, DeleteBehavior.Cascade);
        AssertIncidentDeleteBehavior<IncidentSupport>(dbContext, DeleteBehavior.Cascade);
        AssertIncidentDeleteBehavior<IncidentReportLink>(dbContext, DeleteBehavior.Cascade);
        AssertIncidentDeleteBehavior<IncidentEvent>(dbContext, DeleteBehavior.Cascade);
        AssertIncidentDeleteBehavior<IncidentSubscription>(dbContext, DeleteBehavior.Cascade);
        AssertIncidentDeleteBehavior<IncidentSla>(dbContext, DeleteBehavior.Cascade);
        AssertIncidentDeleteBehavior<FeedbackProviderReport>(dbContext, DeleteBehavior.Restrict);
        AssertIncidentDeleteBehavior<CompletionDocument>(dbContext, DeleteBehavior.Restrict);
        AssertIncidentDeleteBehavior<FeedbackResolution>(dbContext, DeleteBehavior.Restrict);
        AssertIncidentDeleteBehavior<Notification>(dbContext, DeleteBehavior.SetNull);

        var incidentEntity = dbContext.Model.FindEntityType(typeof(Incident))!;
        var mergedIntoForeignKey = incidentEntity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Single().Name == nameof(Incident.MergedIntoIncidentId));
        Assert.Equal(DeleteBehavior.Restrict, mergedIntoForeignKey.DeleteBehavior);
    }

    private static void AssertIncidentDeleteBehavior<TEntity>(
        UrbanServiceDbContext dbContext,
        DeleteBehavior expectedBehavior)
        where TEntity : class
    {
        var entity = dbContext.Model.FindEntityType(typeof(TEntity))!;
        var incidentForeignKey = entity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Single().Name == nameof(Incident.IncidentId));
        Assert.Equal(expectedBehavior, incidentForeignKey.DeleteBehavior);
    }

    private static ManagementIncidentsController CreateController(IIncidentService incidentService)
    {
        return new ManagementIncidentsController(
            incidentService,
            Substitute.For<IFeedbackService>(),
            Substitute.For<ICloudinaryService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static Incident CreateIncident(Guid incidentId, Guid? mergedIntoIncidentId = null)
    {
        return new Incident
        {
            IncidentId = incidentId,
            MergedIntoIncidentId = mergedIntoIncidentId,
            Title = $"Incident {incidentId}",
            LocationText = "District 1",
            Severity = IncidentSeverity.Medium,
            Status = mergedIntoIncidentId.HasValue ? IncidentStatus.Merged : IncidentStatus.New,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class DeleteTestContext
    {
        public DeleteTestContext()
        {
            ConfigureRepository(IncidentRepository, Incidents);
            ConfigureRepository(CompletionDocumentRepository, CompletionDocuments);
            ConfigureRepository(ResolutionRepository, Resolutions);
            ConfigureRepository(ProviderReportRepository, ProviderReports);
            ConfigureRepository(NotificationRepository, Notifications);

            UnitOfWork.GetRepository<Incident>().Returns(IncidentRepository);
            UnitOfWork.GetRepository<CompletionDocument>().Returns(CompletionDocumentRepository);
            UnitOfWork.GetRepository<FeedbackResolution>().Returns(ResolutionRepository);
            UnitOfWork.GetRepository<FeedbackProviderReport>().Returns(ProviderReportRepository);
            UnitOfWork.GetRepository<Notification>().Returns(NotificationRepository);
            UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
        }

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public IGenericRepository<Incident> IncidentRepository { get; } = Substitute.For<IGenericRepository<Incident>>();
        public IGenericRepository<CompletionDocument> CompletionDocumentRepository { get; } = Substitute.For<IGenericRepository<CompletionDocument>>();
        public IGenericRepository<FeedbackResolution> ResolutionRepository { get; } = Substitute.For<IGenericRepository<FeedbackResolution>>();
        public IGenericRepository<FeedbackProviderReport> ProviderReportRepository { get; } = Substitute.For<IGenericRepository<FeedbackProviderReport>>();
        public IGenericRepository<Notification> NotificationRepository { get; } = Substitute.For<IGenericRepository<Notification>>();

        public List<Incident> Incidents { get; } = [];
        public List<CompletionDocument> CompletionDocuments { get; } = [];
        public List<FeedbackResolution> Resolutions { get; } = [];
        public List<FeedbackProviderReport> ProviderReports { get; } = [];
        public List<Notification> Notifications { get; } = [];

        private static void ConfigureRepository<T>(
            IGenericRepository<T> repository,
            List<T> entities)
            where T : class
        {
            repository.Entities.Returns(_ => entities.AsAsyncQueryable());
            repository.When(instance => instance.DeleteRange(Arg.Any<IEnumerable<T>>()))
                .Do(call =>
                {
                    foreach (var entity in call.Arg<IEnumerable<T>>().ToArray())
                    {
                        entities.Remove(entity);
                    }
                });
        }
    }
}
