using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Data;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public sealed class ManagerAreaAssignmentServiceTests
{
    [Fact]
    public async Task GetAssignmentsAsync_FiltersAndProjectsAssignmentDetails()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        var selectedManager = context.User(
            UserRole.INTERACTIONMANAGER,
            "Selected Manager",
            email: "selected.manager@urban.test");
        var otherManager = context.User(UserRole.INTERACTIONMANAGER, "Other Manager");
        var selectedArea = context.Area("Ward One", "WARD-001");
        var otherArea = context.Area("Ward Two", "WARD-002");
        var expected = context.Assignment(
            selectedManager,
            selectedArea,
            admin,
            isActive: true,
            updatedBy: admin);
        context.Assignment(selectedManager, otherArea, admin, isActive: false);
        context.Assignment(otherManager, selectedArea, admin, isActive: true);
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);

        var result = await service.GetAssignmentsAsync(new ManagerAreaAssignmentQueryParameters
        {
            ManagerUserId = selectedManager.UserId,
            AreaId = selectedArea.AreaId,
            IsActive = true
        });

        var dto = Assert.Single(result);
        Assert.Equal(expected.ManagerAreaAssignmentId, dto.ManagerAreaAssignmentId);
        Assert.Equal(selectedManager.UserId, dto.ManagerUserId);
        Assert.Equal(selectedManager.FullName, dto.ManagerName);
        Assert.Equal(selectedManager.Email, dto.ManagerEmail);
        Assert.True(dto.ManagerIsActive);
        Assert.Equal(selectedArea.AreaId, dto.AreaId);
        Assert.Equal(selectedArea.AreaName, dto.AreaName);
        Assert.Equal(selectedArea.WardCode, dto.WardCode);
        Assert.True(dto.AreaIsActive);
        Assert.Equal(admin.UserId, dto.CreatedByUserId);
        Assert.Equal(admin.FullName, dto.CreatedByUserName);
        Assert.Equal(admin.UserId, dto.UpdatedByUserId);
        Assert.Equal(admin.FullName, dto.UpdatedByUserName);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateAsync_ValidManagerAndArea_CreatesActiveAssignmentWithAudit()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var area = context.Area("Ward One", "WARD-001");
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);
        var before = DateTime.UtcNow;

        var result = await service.CreateAsync(admin.UserId, new ManagerAreaAssignmentCreateRequest
        {
            ManagerUserId = manager.UserId,
            AreaId = area.AreaId
        });

        var assignment = Assert.Single(context.Assignments);
        Assert.Equal(manager.UserId, assignment.ManagerUserId);
        Assert.Equal(area.AreaId, assignment.AreaId);
        Assert.Equal(admin.UserId, assignment.CreatedByUserId);
        Assert.Null(assignment.UpdatedByUserId);
        Assert.True(assignment.IsActive);
        Assert.InRange(assignment.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(assignment.ManagerAreaAssignmentId, result.ManagerAreaAssignmentId);
        Assert.Equal(manager.FullName, result.ManagerName);
        Assert.Equal(area.AreaName, result.AreaName);
        Assert.Equal(admin.FullName, result.CreatedByUserName);
        await context.AssignmentRepository.Received(1).AddAsync(assignment);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task CreateAsync_AdminRoleDiffersOnlyByCase_CreatesAssignment()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var admin = context.User("SystemAdmin", "System Admin");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var area = context.Area("Ward One", "WARD-001");
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);

        var result = await service.CreateAsync(admin.UserId, new ManagerAreaAssignmentCreateRequest
        {
            ManagerUserId = manager.UserId,
            AreaId = area.AreaId
        });

        Assert.Equal(admin.UserId, result.CreatedByUserId);
        Assert.True(result.IsActive);
        await context.AssignmentRepository.Received(1)
            .AddAsync(Arg.Any<ManagerAreaAssignment>());
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task CreateAsync_ManagerRoleDiffersOnlyByCase_CreatesAssignment()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        var manager = context.User("InteractionManager", "Ward Manager");
        var area = context.Area("Ward One", "WARD-001");
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);

        var result = await service.CreateAsync(admin.UserId, new ManagerAreaAssignmentCreateRequest
        {
            ManagerUserId = manager.UserId,
            AreaId = area.AreaId
        });

        Assert.Equal(manager.UserId, result.ManagerUserId);
        Assert.True(result.IsActive);
        await context.AssignmentRepository.Received(1)
            .AddAsync(Arg.Any<ManagerAreaAssignment>());
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task CreateAsync_ExistingInactiveAssignment_ReactivatesWithoutAddingDuplicate()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var originalAdmin = context.User(UserRole.SYSTEMADMIN, "Original Admin");
        var currentAdmin = context.User(UserRole.SYSTEMADMIN, "Current Admin");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var area = context.Area("Ward One", "WARD-001");
        var existing = context.Assignment(manager, area, originalAdmin, isActive: false);
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);
        var before = DateTime.UtcNow;

        var result = await service.CreateAsync(currentAdmin.UserId, new ManagerAreaAssignmentCreateRequest
        {
            ManagerUserId = manager.UserId,
            AreaId = area.AreaId
        });

        Assert.Single(context.Assignments);
        Assert.Same(existing, context.Assignments[0]);
        Assert.True(existing.IsActive);
        Assert.Equal(currentAdmin.UserId, existing.UpdatedByUserId);
        Assert.NotNull(existing.UpdatedAt);
        Assert.InRange(existing.UpdatedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(existing.ManagerAreaAssignmentId, result.ManagerAreaAssignmentId);
        Assert.Equal(currentAdmin.FullName, result.UpdatedByUserName);
        await context.AssignmentRepository.DidNotReceive()
            .AddAsync(Arg.Any<ManagerAreaAssignment>());
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task CreateAsync_UserHasWrongRole_RejectsWithoutSaving()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        var staff = context.User(UserRole.SYSTEMSTAFF, "System Staff");
        var area = context.Area("Ward One", "WARD-001");
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<Exception>(() => service.CreateAsync(
            admin.UserId,
            new ManagerAreaAssignmentCreateRequest
            {
                ManagerUserId = staff.UserId,
                AreaId = area.AreaId
            }));

        Assert.Empty(context.Assignments);
        await context.AssignmentRepository.DidNotReceive()
            .AddAsync(Arg.Any<ManagerAreaAssignment>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task CreateAsync_NonAdminActor_IsForbidden()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var staff = context.User(UserRole.SYSTEMSTAFF, "System Staff");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var area = context.Area("Ward One", "WARD-001");
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateAsync(
            staff.UserId,
            new ManagerAreaAssignmentCreateRequest
            {
                ManagerUserId = manager.UserId,
                AreaId = area.AreaId
            }));

        Assert.Empty(context.Assignments);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task CreateAsync_AreaIsInactive_RejectsWithoutSaving()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var area = context.Area("Inactive Ward", "WARD-001", isActive: false);
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<Exception>(() => service.CreateAsync(
            admin.UserId,
            new ManagerAreaAssignmentCreateRequest
            {
                ManagerUserId = manager.UserId,
                AreaId = area.AreaId
            }));

        Assert.Empty(context.Assignments);
        await context.AssignmentRepository.DidNotReceive()
            .AddAsync(Arg.Any<ManagerAreaAssignment>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task UpdateAsync_DuplicateManagerArea_RejectsWithoutSaving()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var firstArea = context.Area("Ward One", "WARD-001");
        var duplicateArea = context.Area("Ward Two", "WARD-002");
        var assignment = context.Assignment(manager, firstArea, admin, isActive: true);
        context.Assignment(manager, duplicateArea, admin, isActive: true);
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateAsync(
            admin.UserId,
            assignment.ManagerAreaAssignmentId,
            new ManagerAreaAssignmentUpdateRequest { AreaId = duplicateArea.AreaId }));

        Assert.Equal(firstArea.AreaId, assignment.AreaId);
        Assert.Null(assignment.UpdatedByUserId);
        Assert.Null(assignment.UpdatedAt);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task UpdateAsync_ValidArea_ChangesScopeAndUpdatesAudit()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var originalAdmin = context.User(UserRole.SYSTEMADMIN, "Original Admin");
        var currentAdmin = context.User(UserRole.SYSTEMADMIN, "Current Admin");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var originalArea = context.Area("Ward One", "WARD-001");
        var newArea = context.Area("Ward Two", "WARD-002");
        var assignment = context.Assignment(manager, originalArea, originalAdmin, isActive: true);
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);
        var before = DateTime.UtcNow;

        var result = await service.UpdateAsync(
            currentAdmin.UserId,
            assignment.ManagerAreaAssignmentId,
            new ManagerAreaAssignmentUpdateRequest { AreaId = newArea.AreaId });

        Assert.Equal(newArea.AreaId, assignment.AreaId);
        Assert.Equal(currentAdmin.UserId, assignment.UpdatedByUserId);
        Assert.NotNull(assignment.UpdatedAt);
        Assert.InRange(assignment.UpdatedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(newArea.AreaId, result.AreaId);
        Assert.Equal(newArea.AreaName, result.AreaName);
        Assert.Equal(currentAdmin.FullName, result.UpdatedByUserName);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task SetActiveAsync_ExistingAssignment_ActivatesAndUpdatesAudit()
    {
        var context = new ManagerAreaAssignmentTestContext();
        var originalAdmin = context.User(UserRole.SYSTEMADMIN, "Original Admin");
        var currentAdmin = context.User(UserRole.SYSTEMADMIN, "Current Admin");
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var area = context.Area("Ward One", "WARD-001");
        var assignment = context.Assignment(manager, area, originalAdmin, isActive: false);
        var service = new ManagerAreaAssignmentService(context.UnitOfWork);
        var before = DateTime.UtcNow;

        var result = await service.SetActiveAsync(
            currentAdmin.UserId,
            assignment.ManagerAreaAssignmentId,
            isActive: true);

        Assert.True(assignment.IsActive);
        Assert.Equal(currentAdmin.UserId, assignment.UpdatedByUserId);
        Assert.NotNull(assignment.UpdatedAt);
        Assert.InRange(assignment.UpdatedAt!.Value, before, DateTime.UtcNow);
        Assert.True(result.IsActive);
        Assert.Equal(currentAdmin.FullName, result.UpdatedByUserName);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public void AdminController_RequiresSystemAdminAndUsesExpectedRoutes()
    {
        var controllerType = typeof(AdminManagerAreaAssignmentsController);
        var authorize = Assert.Single(controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(UserRole.SYSTEMADMIN, authorize.Roles);

        var controllerRoute = Assert.Single(controllerType
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>());
        Assert.Equal("api/admin/manager-area-assignments", controllerRoute.Template);

        AssertHttpMethod<HttpGetAttribute>(
            nameof(AdminManagerAreaAssignmentsController.GetAssignments),
            expectedTemplate: null);
        AssertHttpMethod<HttpGetAttribute>(
            nameof(AdminManagerAreaAssignmentsController.GetAssignment),
            "{assignmentId:int}");
        AssertHttpMethod<HttpPostAttribute>(
            nameof(AdminManagerAreaAssignmentsController.CreateAssignment),
            expectedTemplate: null);
        AssertHttpMethod<HttpPutAttribute>(
            nameof(AdminManagerAreaAssignmentsController.UpdateAssignment),
            "{assignmentId:int}");
        AssertHttpMethod<HttpPatchAttribute>(
            nameof(AdminManagerAreaAssignmentsController.SetActive),
            "{assignmentId:int}/active");
    }

    [Fact]
    public void DbContextModel_ConfiguresUniqueManagerAreaScope()
    {
        var options = new DbContextOptionsBuilder<UrbanServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=urbanservice_model_test")
            .Options;
        using var dbContext = new UrbanServiceDbContext(options);

        var entityType = dbContext.Model.FindEntityType(typeof(ManagerAreaAssignment))!;
        var scopeIndex = Assert.Single(entityType.GetIndexes().Where(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(ManagerAreaAssignment.ManagerUserId), nameof(ManagerAreaAssignment.AreaId)])));

        Assert.True(scopeIndex.IsUnique);
    }

    private static void AssertHttpMethod<TAttribute>(string actionName, string? expectedTemplate)
        where TAttribute : HttpMethodAttribute
    {
        var action = typeof(AdminManagerAreaAssignmentsController).GetMethod(actionName)!;
        var attribute = Assert.Single(action
            .GetCustomAttributes(typeof(TAttribute), inherit: true)
            .Cast<TAttribute>());
        Assert.Equal(expectedTemplate, attribute.Template);
    }

    private sealed class ManagerAreaAssignmentTestContext
    {
        private int _nextAssignmentId = 1;
        private int _nextAreaId = 1;
        private int _nextRoleId = 1;

        public ManagerAreaAssignmentTestContext()
        {
            AssignmentRepository.Entities.Returns(_ => Assignments.AsAsyncQueryable());
            UserRepository.Entities.Returns(_ => Users.AsAsyncQueryable());
            AreaRepository.Entities.Returns(_ => Areas.AsAsyncQueryable());

            AssignmentRepository.AddAsync(Arg.Any<ManagerAreaAssignment>())
                .Returns(call =>
                {
                    var assignment = call.Arg<ManagerAreaAssignment>();
                    if (assignment.ManagerAreaAssignmentId == 0)
                    {
                        assignment.ManagerAreaAssignmentId = _nextAssignmentId++;
                    }

                    AttachNavigations(assignment);
                    Assignments.Add(assignment);
                    return Task.CompletedTask;
                });

            UnitOfWork.GetRepository<ManagerAreaAssignment>().Returns(AssignmentRepository);
            UnitOfWork.GetRepository<User>().Returns(UserRepository);
            UnitOfWork.GetRepository<OperatingArea>().Returns(AreaRepository);
            UnitOfWork.SaveAsync().Returns(_ =>
            {
                foreach (var assignment in Assignments)
                {
                    AttachNavigations(assignment);
                }

                return Task.CompletedTask;
            });
        }

        public List<ManagerAreaAssignment> Assignments { get; } = [];

        public List<User> Users { get; } = [];

        public List<OperatingArea> Areas { get; } = [];

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IGenericRepository<ManagerAreaAssignment> AssignmentRepository { get; } =
            Substitute.For<IGenericRepository<ManagerAreaAssignment>>();

        public IGenericRepository<User> UserRepository { get; } =
            Substitute.For<IGenericRepository<User>>();

        public IGenericRepository<OperatingArea> AreaRepository { get; } =
            Substitute.For<IGenericRepository<OperatingArea>>();

        public User User(
            string roleName,
            string fullName,
            string? email = null,
            bool isActive = true)
        {
            var role = new Role
            {
                RoleId = _nextRoleId++,
                RoleName = roleName,
                Description = $"{roleName} role"
            };
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = role.RoleId,
                Role = role,
                FullName = fullName,
                Email = email ?? $"{Guid.NewGuid()}@urban.test",
                PasswordHash = "test-hash",
                IsActive = isActive,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            role.Users.Add(user);
            Users.Add(user);
            return user;
        }

        public OperatingArea Area(string name, string wardCode, bool isActive = true)
        {
            var area = new OperatingArea
            {
                AreaId = _nextAreaId++,
                AreaName = name,
                AreaType = "Ward",
                WardCode = wardCode,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            };
            Areas.Add(area);
            return area;
        }

        public ManagerAreaAssignment Assignment(
            User manager,
            OperatingArea area,
            User createdBy,
            bool isActive,
            User? updatedBy = null)
        {
            var now = DateTime.UtcNow;
            var assignment = new ManagerAreaAssignment
            {
                ManagerAreaAssignmentId = _nextAssignmentId++,
                ManagerUserId = manager.UserId,
                ManagerUser = manager,
                AreaId = area.AreaId,
                Area = area,
                CreatedByUserId = createdBy.UserId,
                CreatedByUser = createdBy,
                UpdatedByUserId = updatedBy?.UserId,
                UpdatedByUser = updatedBy,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = updatedBy == null ? null : now
            };
            Assignments.Add(assignment);
            return assignment;
        }

        private void AttachNavigations(ManagerAreaAssignment assignment)
        {
            assignment.ManagerUser = Users.Single(user => user.UserId == assignment.ManagerUserId);
            assignment.Area = Areas.Single(area => area.AreaId == assignment.AreaId);
            assignment.CreatedByUser = Users.Single(user => user.UserId == assignment.CreatedByUserId);
            assignment.UpdatedByUser = assignment.UpdatedByUserId.HasValue
                ? Users.Single(user => user.UserId == assignment.UpdatedByUserId.Value)
                : null;
        }
    }
}
