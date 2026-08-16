using NSubstitute;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Services;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;
using Xunit;

namespace UrbanService.BLL.Tests;

public class UserManagementServiceTests
{
    [Fact]
    public async Task GetUsersAsync_FiltersBySearchRoleAndStatus()
    {
        var context = new UserManagementTestContext();
        var adminRole = context.Role(1, "Admin");
        var staffRole = context.Role(2, "Staff");
        var matchingUser = context.User(
            "Nguyen Van Staff",
            "staff@urban.test",
            staffRole,
            isActive: true,
            isVerified: true,
            createdAt: DateTime.UtcNow.AddHours(-1));
        context.User(
            "Tran Van Staff",
            "inactive@urban.test",
            staffRole,
            isActive: false,
            isVerified: true,
            createdAt: DateTime.UtcNow);
        context.User(
            "Le Thi Admin",
            "admin@urban.test",
            adminRole,
            isActive: true,
            isVerified: true,
            createdAt: DateTime.UtcNow.AddHours(-2));
        var service = new UserManagementService(context.UnitOfWork);

        var result = await service.GetUsersAsync(new UserQueryParameters
        {
            Search = "staff",
            RoleName = "staff",
            IsActive = true,
            IsVerified = true
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(matchingUser.UserId, item.UserId);
        Assert.Equal("Staff", item.RoleName);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task CreateUserAsync_NormalizesEmailAndOptionalFields()
    {
        var context = new UserManagementTestContext();
        var staffRole = context.Role(2, "Staff");
        var service = new UserManagementService(context.UnitOfWork);

        var created = await service.CreateUserAsync(new AdminCreateUserRequest
        {
            RoleId = staffRole.RoleId,
            FullName = "  Nguyen Van Staff  ",
            Email = "  Staff@Urban.Test  ",
            Password = "secret123",
            PhoneNumber = "  0909000000  ",
            Address = "   ",
            AvatarUrl = "  https://cdn.test/avatar.png  ",
            IsActive = true,
            IsVerified = false
        });

        Assert.Equal("Nguyen Van Staff", created.FullName);
        Assert.Equal("staff@urban.test", created.Email);
        Assert.Equal("0909000000", created.PhoneNumber);
        Assert.Null(created.Address);
        Assert.Equal("https://cdn.test/avatar.png", created.AvatarUrl);
        Assert.False(created.IsVerified);
        Assert.NotEqual("secret123", context.Users.Single().PasswordHash);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    [Fact]
    public async Task CreateUserAsync_RejectsDuplicateEmailIgnoringCase()
    {
        var context = new UserManagementTestContext();
        var staffRole = context.Role(2, "Staff");
        context.User("Existing Staff", "staff@urban.test", staffRole);
        var service = new UserManagementService(context.UnitOfWork);

        await Assert.ThrowsAsync<Exception>(() => service.CreateUserAsync(
            new AdminCreateUserRequest
            {
                RoleId = staffRole.RoleId,
                FullName = "Duplicate Staff",
                Email = "STAFF@URBAN.TEST",
                Password = "secret123"
            }));

        Assert.Single(context.Users);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task UpdateUserAsync_BlocksAdminFromDeactivatingOwnAccount()
    {
        var context = new UserManagementTestContext();
        var adminRole = context.Role(1, "Admin");
        var admin = context.User("System Admin", "admin@urban.test", adminRole);
        var service = new UserManagementService(context.UnitOfWork);

        await Assert.ThrowsAsync<Exception>(() => service.UpdateUserAsync(
            admin.UserId,
            admin.UserId,
            new AdminUpdateUserRequest { IsActive = false }));

        Assert.True(admin.IsActive);
        await context.UnitOfWork.DidNotReceive().SaveAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_HashesPasswordAndRevokesRefreshToken()
    {
        var context = new UserManagementTestContext();
        var citizenRole = context.Role(3, "Citizen");
        var user = context.User(
            "Citizen One",
            "citizen@urban.test",
            citizenRole,
            isRefreshTokenRevoked: false);
        var originalHash = user.PasswordHash;
        var service = new UserManagementService(context.UnitOfWork);

        await service.ResetPasswordAsync(
            user.UserId,
            new AdminResetUserPasswordRequest { NewPassword = "new-secret" });

        Assert.NotEqual(originalHash, user.PasswordHash);
        Assert.True(user.IsRefreshTokenRevoked);
        Assert.NotNull(user.UpdatedAt);
        await context.UnitOfWork.Received(1).SaveAsync();
    }

    private sealed class UserManagementTestContext
    {
        public UserManagementTestContext()
        {
            UserRepository.Entities.Returns(_ => Users.AsAsyncQueryable());
            UserRepository.AddAsync(Arg.Any<User>())
                .Returns(call =>
                {
                    var user = call.Arg<User>();
                    user.Role = Roles.Single(role => role.RoleId == user.RoleId);
                    user.Role.Users.Add(user);
                    Users.Add(user);
                    return Task.CompletedTask;
                });

            RoleRepository.Entities.Returns(_ => Roles.AsAsyncQueryable());

            UnitOfWork.GetRepository<User>().Returns(UserRepository);
            UnitOfWork.GetRepository<Role>().Returns(RoleRepository);
            UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
        }

        public List<User> Users { get; } = [];

        public List<Role> Roles { get; } = [];

        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

        public IGenericRepository<User> UserRepository { get; } =
            Substitute.For<IGenericRepository<User>>();

        public IGenericRepository<Role> RoleRepository { get; } =
            Substitute.For<IGenericRepository<Role>>();

        public Role Role(int roleId, string roleName)
        {
            var role = new Role
            {
                RoleId = roleId,
                RoleName = roleName,
                Description = $"{roleName} role"
            };
            Roles.Add(role);
            return role;
        }

        public User User(
            string fullName,
            string email,
            Role role,
            bool isActive = true,
            bool isVerified = true,
            bool isRefreshTokenRevoked = false,
            DateTime? createdAt = null)
        {
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = role.RoleId,
                Role = role,
                FullName = fullName,
                Email = email,
                PasswordHash = "existing-hash",
                IsActive = isActive,
                IsVerified = isVerified,
                IsRefreshTokenRevoked = isRefreshTokenRevoked,
                CreatedAt = createdAt ?? DateTime.UtcNow,
                UpdatedAt = createdAt ?? DateTime.UtcNow
            };
            Users.Add(user);
            role.Users.Add(user);
            return user;
        }
    }
}
