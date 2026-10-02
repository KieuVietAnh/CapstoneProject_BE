using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NSubstitute;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Securities;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Services;
using UrbanService.Controllers;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public sealed class StaffAreaAssignmentServiceTests
{
    [Fact]
    public async Task MixedCaseManagerWithinCoveredAreas_CanViewCreateUpdateAndDeactivate()
    {
        var context = new StaffAreaAssignmentTestContext();
        var manager = context.User("InteractionManager", "Ward Manager");
        var staff = context.User("SystemStaff", "System Staff");
        var firstArea = context.Area("Ward One");
        var secondArea = context.Area("Ward Two");
        var otherArea = context.Area("Ward Three");
        context.ManagerCoverage(manager, firstArea);
        context.ManagerCoverage(manager, secondArea);
        context.Assignment(staff, otherArea, manager);
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        var visibleBeforeCreate = await service.GetAssignmentsAsync(manager.UserId);
        var created = await service.CreateAsync(
            manager.UserId,
            new StaffAreaAssignmentCreateRequest
            {
                UserId = staff.UserId,
                AreaId = firstArea.AreaId,
                IsPrimary = true
            });
        var updated = await service.UpdateAsync(
            manager.UserId,
            created.StaffAreaAssignmentId,
            new StaffAreaAssignmentUpdateRequest
            {
                AreaId = secondArea.AreaId,
                IsPrimary = false
            });
        var deactivated = await service.SetActiveAsync(
            manager.UserId,
            created.StaffAreaAssignmentId,
            isActive: false);

        Assert.Empty(visibleBeforeCreate);
        Assert.Equal(manager.UserId, created.AssignedByUserId);
        Assert.Equal(firstArea.AreaId, created.AreaId);
        Assert.Equal(secondArea.AreaId, updated.AreaId);
        Assert.False(updated.IsPrimary);
        Assert.False(deactivated.IsActive);
        await context.UnitOfWork.Received(3).SaveAsync();
    }

    [Fact]
    public async Task ManagerWithinCoveredArea_CanCreateNewStaffAccountAndInitialAssignment()
    {
        var context = new StaffAreaAssignmentTestContext();
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var staffRole = context.Role(UserRole.SYSTEMSTAFF);
        var area = context.Area("Ward One");
        var category = context.Category("Roads");
        context.ManagerCoverage(manager, area);
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        var managedAreas = await service.GetManagedAreasAsync(manager.UserId);
        var created = await service.CreateStaffAccountAsync(
            manager.UserId,
            new ManagedStaffAccountCreateRequest
            {
                FullName = "New Staff",
                Email = " New.Staff@Urban.Test ",
                Password = "password-123",
                PhoneNumber = "0901 234 567",
                Address = " Ward office ",
                AreaId = area.AreaId,
                CategoryId = category.CategoryId,
                IsPrimary = true
            });

        var user = Assert.Single(context.Users.Where(item => item.UserId == created.UserId));
        Assert.Single(managedAreas);
        Assert.Equal(area.AreaId, managedAreas.Single().AreaId);
        Assert.Equal(staffRole.RoleId, user.RoleId);
        Assert.Equal(UserRole.SYSTEMSTAFF, created.RoleName);
        Assert.Equal("new.staff@urban.test", created.Email);
        Assert.Equal("+84901234567", created.PhoneNumber);
        Assert.Equal("Ward office", created.Address);
        Assert.True(created.IsActive);
        Assert.True(created.IsVerified);
        Assert.True(PasswordHasher.Verify("password-123", user.PasswordHash));
        Assert.Equal(area.AreaId, created.Assignment.AreaId);
        Assert.Equal(category.CategoryId, created.Assignment.CategoryId);
        Assert.Equal(manager.UserId, created.Assignment.AssignedByUserId);
        Assert.True(created.Assignment.IsPrimary);
        context.UnitOfWork.Received(1).BeginTransaction();
        context.UnitOfWork.Received(1).CommitTransaction();
        context.UnitOfWork.DidNotReceive().RollBack();
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task ManagerOutsideCoveredArea_CannotCreateNewStaffAccount()
    {
        var context = new StaffAreaAssignmentTestContext();
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        context.Role(UserRole.SYSTEMSTAFF);
        var coveredArea = context.Area("Covered Ward");
        var otherArea = context.Area("Other Ward");
        context.ManagerCoverage(manager, coveredArea);
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateStaffAccountAsync(
            manager.UserId,
            new ManagedStaffAccountCreateRequest
            {
                FullName = "New Staff",
                Email = "staff@urban.test",
                Password = "password-123",
                AreaId = otherArea.AreaId
            }));

        context.UnitOfWork.DidNotReceive().BeginTransaction();
        await context.UserRepository.DidNotReceive().AddAsync(Arg.Any<User>());
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task AdminCannotUseManagerStaffAccountCreationService()
    {
        var context = new StaffAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        context.Role(UserRole.SYSTEMSTAFF);
        var area = context.Area("Ward One");
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateStaffAccountAsync(
            admin.UserId,
            new ManagedStaffAccountCreateRequest
            {
                FullName = "New Staff",
                Email = "staff@urban.test",
                Password = "password-123",
                AreaId = area.AreaId
            }));

        context.UnitOfWork.DidNotReceive().BeginTransaction();
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ManagerOutsideAssignedArea_CannotViewCreateUpdateOrDeactivate()
    {
        var context = new StaffAreaAssignmentTestContext();
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var staff = context.User(UserRole.SYSTEMSTAFF, "System Staff");
        var coveredArea = context.Area("Covered Ward");
        var otherArea = context.Area("Other Ward");
        context.ManagerCoverage(manager, coveredArea);
        var otherAssignment = context.Assignment(staff, otherArea, manager);
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        var visible = await service.GetAssignmentsAsync(manager.UserId);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateAsync(
            manager.UserId,
            new StaffAreaAssignmentCreateRequest
            {
                UserId = staff.UserId,
                AreaId = otherArea.AreaId
            }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateAsync(
            manager.UserId,
            otherAssignment.StaffAreaAssignmentId,
            new StaffAreaAssignmentUpdateRequest { AreaId = coveredArea.AreaId }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.SetActiveAsync(
            manager.UserId,
            otherAssignment.StaffAreaAssignmentId,
            isActive: false));

        Assert.Empty(visible);
        Assert.True(otherAssignment.IsActive);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task Manager_CannotReactivateAssignment()
    {
        var context = new StaffAreaAssignmentTestContext();
        var manager = context.User(UserRole.INTERACTIONMANAGER, "Ward Manager");
        var staff = context.User(UserRole.SYSTEMSTAFF, "System Staff");
        var area = context.Area("Ward One");
        context.ManagerCoverage(manager, area);
        var assignment = context.Assignment(staff, area, manager, isActive: false);
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.SetActiveAsync(
            manager.UserId,
            assignment.StaffAreaAssignmentId,
            isActive: true));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateAsync(
            manager.UserId,
            new StaffAreaAssignmentCreateRequest
            {
                UserId = staff.UserId,
                AreaId = area.AreaId
            }));

        Assert.False(assignment.IsActive);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task AdminAcrossAllAreas_CanViewCreateUpdateAndDeactivate()
    {
        var context = new StaffAreaAssignmentTestContext();
        var admin = context.User(UserRole.SYSTEMADMIN, "System Admin");
        var staff = context.User(UserRole.SYSTEMSTAFF, "System Staff");
        var firstArea = context.Area("Ward One");
        var secondArea = context.Area("Ward Two");
        context.Assignment(staff, firstArea, admin);
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        var visible = await service.GetAssignmentsAsync(admin.UserId);
        var created = await service.CreateAsync(
            admin.UserId,
            new StaffAreaAssignmentCreateRequest
            {
                UserId = staff.UserId,
                AreaId = secondArea.AreaId,
                IsPrimary = true
            });
        var updated = await service.UpdateAsync(
            admin.UserId,
            created.StaffAreaAssignmentId,
            new StaffAreaAssignmentUpdateRequest
            {
                AreaId = secondArea.AreaId,
                IsPrimary = false
            });
        var deactivated = await service.SetActiveAsync(
            admin.UserId,
            created.StaffAreaAssignmentId,
            isActive: false);
        var reactivated = await service.SetActiveAsync(
            admin.UserId,
            created.StaffAreaAssignmentId,
            isActive: true);

        Assert.Single(visible);
        Assert.Equal(admin.UserId, created.AssignedByUserId);
        Assert.False(updated.IsPrimary);
        Assert.False(deactivated.IsActive);
        Assert.True(reactivated.IsActive);
        await context.UnitOfWork.Received(4).SaveAsync();
    }

    [Fact]
    public async Task SystemStaff_CannotViewOrManageAssignments()
    {
        var context = new StaffAreaAssignmentTestContext();
        var staffActor = context.User(UserRole.SYSTEMSTAFF, "Staff Actor");
        var targetStaff = context.User(UserRole.SYSTEMSTAFF, "Target Staff");
        var area = context.Area("Ward One");
        var assignment = context.Assignment(targetStaff, area, staffActor);
        var service = new StaffAreaAssignmentService(context.UnitOfWork);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.GetAssignmentsAsync(staffActor.UserId));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.CreateAsync(
            staffActor.UserId,
            new StaffAreaAssignmentCreateRequest
            {
                UserId = targetStaff.UserId,
                AreaId = area.AreaId
            }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.UpdateAsync(
            staffActor.UserId,
            assignment.StaffAreaAssignmentId,
            new StaffAreaAssignmentUpdateRequest { AreaId = area.AreaId }));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.SetActiveAsync(
            staffActor.UserId,
            assignment.StaffAreaAssignmentId,
            isActive: false));

        Assert.True(assignment.IsActive);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public void Controller_PreservesRoutesAndAllowsOnlyAdminAndManager()
    {
        var controllerType = typeof(ManagementStaffAreaAssignmentsController);
        var authorize = Assert.Single(controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(
            UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER,
            authorize.Roles);

        var routes = controllerType
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(attribute => attribute.Template)
            .ToHashSet();
        Assert.Equal(2, routes.Count);
        Assert.Contains("api/management/staff-area-assignments", routes);
        Assert.Contains("api/management/staff-responsibilities", routes);

        AssertHttpMethod<HttpGetAttribute>(
            nameof(ManagementStaffAreaAssignmentsController.GetAssignments),
            expectedTemplate: null);
        AssertHttpMethod<HttpGetAttribute>(
            nameof(ManagementStaffAreaAssignmentsController.GetManagedAreas),
            "managed-areas");
        AssertActionRoles(
            nameof(ManagementStaffAreaAssignmentsController.GetManagedAreas),
            UserRole.INTERACTIONMANAGER);
        AssertHttpMethod<HttpPostAttribute>(
            nameof(ManagementStaffAreaAssignmentsController.CreateStaffAccount),
            "staff-accounts");
        AssertActionRoles(
            nameof(ManagementStaffAreaAssignmentsController.CreateStaffAccount),
            UserRole.INTERACTIONMANAGER);
        AssertHttpMethod<HttpPostAttribute>(
            nameof(ManagementStaffAreaAssignmentsController.CreateAssignment),
            expectedTemplate: null);
        AssertHttpMethod<HttpPutAttribute>(
            nameof(ManagementStaffAreaAssignmentsController.UpdateAssignment),
            "{assignmentId:int}");
        AssertHttpMethod<HttpPatchAttribute>(
            nameof(ManagementStaffAreaAssignmentsController.SetActive),
            "{assignmentId:int}/active");
    }

    private static void AssertHttpMethod<TAttribute>(string actionName, string? expectedTemplate)
        where TAttribute : HttpMethodAttribute
    {
        var action = typeof(ManagementStaffAreaAssignmentsController).GetMethod(actionName)!;
        var attribute = Assert.Single(action
            .GetCustomAttributes(typeof(TAttribute), inherit: true)
            .Cast<TAttribute>());
        Assert.Equal(expectedTemplate, attribute.Template);
    }

    private static void AssertActionRoles(string actionName, string expectedRoles)
    {
        var action = typeof(ManagementStaffAreaAssignmentsController).GetMethod(actionName)!;
        var authorize = Assert.Single(action
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(expectedRoles, authorize.Roles);
    }

    private sealed class StaffAreaAssignmentTestContext
    {
        private int _nextAssignmentId = 1;
        private int _nextAreaId = 1;
        private int _nextCategoryId = 1;
        private int _nextManagerAssignmentId = 1;
        private int _nextRoleId = 1;

        public StaffAreaAssignmentTestContext()
        {
            StaffAssignmentRepository.Entities.Returns(
                _ => StaffAssignments.AsAsyncQueryable());
            ManagerAssignmentRepository.Entities.Returns(
                _ => ManagerAssignments.AsAsyncQueryable());
            UserRepository.Entities.Returns(_ => Users.AsAsyncQueryable());
            RoleRepository.Entities.Returns(_ => Roles.AsAsyncQueryable());
            AreaRepository.Entities.Returns(_ => Areas.AsAsyncQueryable());
            CategoryRepository.Entities.Returns(_ => Categories.AsAsyncQueryable());

            UserRepository.AddAsync(Arg.Any<User>())
                .Returns(call =>
                {
                    Users.Add(call.Arg<User>());
                    return Task.CompletedTask;
                });

            StaffAssignmentRepository.AddAsync(Arg.Any<StaffAreaAssignment>())
                .Returns(call =>
                {
                    var assignment = call.Arg<StaffAreaAssignment>();
                    if (assignment.StaffAreaAssignmentId == 0)
                    {
                        assignment.StaffAreaAssignmentId = _nextAssignmentId++;
                    }

                    AttachNavigations(assignment);
                    StaffAssignments.Add(assignment);
                    return Task.CompletedTask;
                });

            UnitOfWork.GetRepository<StaffAreaAssignment>()
                .Returns(StaffAssignmentRepository);
            UnitOfWork.GetRepository<ManagerAreaAssignment>()
                .Returns(ManagerAssignmentRepository);
            UnitOfWork.GetRepository<User>().Returns(UserRepository);
            UnitOfWork.GetRepository<Role>().Returns(RoleRepository);
            UnitOfWork.GetRepository<OperatingArea>().Returns(AreaRepository);
            UnitOfWork.GetRepository<UrbanServiceCategory>().Returns(CategoryRepository);
            UnitOfWork.SaveAsync().Returns(_ =>
            {
                foreach (var assignment in StaffAssignments)
                {
                    AttachNavigations(assignment);
                }

                return Task.CompletedTask;
            });
        }

        public List<StaffAreaAssignment> StaffAssignments { get; } = [];

        public List<ManagerAreaAssignment> ManagerAssignments { get; } = [];

        public List<User> Users { get; } = [];

        public List<Role> Roles { get; } = [];

        public List<OperatingArea> Areas { get; } = [];

        public List<UrbanServiceCategory> Categories { get; } = [];

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IGenericRepository<StaffAreaAssignment> StaffAssignmentRepository { get; } =
            Substitute.For<IGenericRepository<StaffAreaAssignment>>();

        public IGenericRepository<ManagerAreaAssignment> ManagerAssignmentRepository { get; } =
            Substitute.For<IGenericRepository<ManagerAreaAssignment>>();

        public IGenericRepository<User> UserRepository { get; } =
            Substitute.For<IGenericRepository<User>>();

        public IGenericRepository<Role> RoleRepository { get; } =
            Substitute.For<IGenericRepository<Role>>();

        public IGenericRepository<OperatingArea> AreaRepository { get; } =
            Substitute.For<IGenericRepository<OperatingArea>>();

        public IGenericRepository<UrbanServiceCategory> CategoryRepository { get; } =
            Substitute.For<IGenericRepository<UrbanServiceCategory>>();

        public User User(string roleName, string fullName)
        {
            var role = Role(roleName);
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = role.RoleId,
                Role = role,
                FullName = fullName,
                Email = $"{Guid.NewGuid()}@urban.test",
                PasswordHash = "test-hash",
                IsActive = true,
                IsVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            role.Users.Add(user);
            Users.Add(user);
            return user;
        }

        public Role Role(string roleName)
        {
            var existing = Roles.FirstOrDefault(role =>
                role.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            var role = new Role
            {
                RoleId = _nextRoleId++,
                RoleName = roleName,
                Description = $"{roleName} role"
            };
            Roles.Add(role);
            return role;
        }

        public OperatingArea Area(string name)
        {
            var area = new OperatingArea
            {
                AreaId = _nextAreaId++,
                AreaName = name,
                AreaType = "Ward",
                WardCode = $"WARD-{_nextAreaId:000}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            Areas.Add(area);
            return area;
        }

        public UrbanServiceCategory Category(string name)
        {
            var category = new UrbanServiceCategory
            {
                CategoryId = _nextCategoryId++,
                CategoryName = name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            Categories.Add(category);
            return category;
        }

        public void ManagerCoverage(User manager, OperatingArea area, bool isActive = true)
        {
            ManagerAssignments.Add(new ManagerAreaAssignment
            {
                ManagerAreaAssignmentId = _nextManagerAssignmentId++,
                ManagerUserId = manager.UserId,
                ManagerUser = manager,
                AreaId = area.AreaId,
                Area = area,
                CreatedByUserId = manager.UserId,
                CreatedByUser = manager,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            });
        }

        public StaffAreaAssignment Assignment(
            User staff,
            OperatingArea area,
            User assignedBy,
            UrbanServiceCategory? category = null,
            bool isActive = true)
        {
            var assignment = new StaffAreaAssignment
            {
                StaffAreaAssignmentId = _nextAssignmentId++,
                UserId = staff.UserId,
                User = staff,
                AreaId = area.AreaId,
                Area = area,
                CategoryId = category?.CategoryId,
                Category = category,
                AssignedByUserId = assignedBy.UserId,
                AssignedByUser = assignedBy,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            };
            StaffAssignments.Add(assignment);
            return assignment;
        }

        private void AttachNavigations(StaffAreaAssignment assignment)
        {
            assignment.User = Users.Single(user => user.UserId == assignment.UserId);
            assignment.Area = Areas.Single(area => area.AreaId == assignment.AreaId);
            assignment.Category = assignment.CategoryId.HasValue
                ? Categories.Single(category => category.CategoryId == assignment.CategoryId.Value)
                : null;
            assignment.AssignedByUser = assignment.AssignedByUserId.HasValue
                ? Users.Single(user => user.UserId == assignment.AssignedByUserId.Value)
                : null;
        }
    }
}
