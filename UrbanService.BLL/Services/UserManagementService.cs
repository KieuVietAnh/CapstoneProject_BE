using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common.Securities;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class UserManagementService : IUserManagementService
{
    private const int MaxPageSize = 100;
    private readonly IUnitOfWork _uow;

    public UserManagementService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<PagedResultDto<AdminUserDto>> GetUsersAsync(UserQueryParameters query)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, MaxPageSize);
        var search = query.Search?.Trim().ToLower();
        var roleName = query.RoleName?.Trim().ToLower();

        var users = _uow.GetRepository<User>().Entities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users.Where(u =>
                u.FullName.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search) ||
                (u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(search)));
        }

        if (query.RoleId.HasValue)
        {
            users = users.Where(u => u.RoleId == query.RoleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            users = users.Where(u => u.Role.RoleName.ToLower() == roleName);
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(u => u.IsActive == query.IsActive.Value);
        }

        if (query.IsVerified.HasValue)
        {
            users = users.Where(u => u.IsVerified == query.IsVerified.Value);
        }

        var totalItems = await users.CountAsync();
        var items = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto
            {
                UserId = u.UserId,
                RoleId = u.RoleId,
                RoleName = u.Role.RoleName,
                OperatorId = u.OperatorId,
                OperatorName = u.Operator != null ? u.Operator.OperatorName : null,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Address = u.Address,
                AvatarUrl = u.AvatarUrl,
                IsActive = u.IsActive,
                IsVerified = u.IsVerified,
                IsRefreshTokenRevoked = u.IsRefreshTokenRevoked,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        return new PagedResultDto<AdminUserDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<AdminUserDto> GetUserAsync(Guid userId)
    {
        var user = await GetUserEntityAsync(userId, asNoTracking: true);
        return MapUser(user);
    }

    public async Task<AdminUserDto> CreateUserAsync(AdminCreateUserRequest request)
    {
        ValidateCreate(request);
        var email = request.Email.Trim().ToLower();

        var exists = await _uow.GetRepository<User>().Entities
            .AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == email);

        if (exists)
        {
            throw new Exception("Email da duoc su dung.");
        }

        await EnsureRoleExistsAsync(request.RoleId);
        await EnsureOperatorExistsAsync(request.OperatorId);

        var now = DateTime.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            RoleId = request.RoleId,
            OperatorId = request.OperatorId,
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            Address = NormalizeOptional(request.Address),
            AvatarUrl = NormalizeOptional(request.AvatarUrl),
            IsActive = request.IsActive,
            IsVerified = request.IsVerified,
            IsRefreshTokenRevoked = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _uow.GetRepository<User>().AddAsync(user);
        await _uow.SaveAsync();

        return await GetUserAsync(user.UserId);
    }

    public async Task<AdminUserDto> UpdateUserAsync(Guid currentAdminId, Guid userId, AdminUpdateUserRequest request)
    {
        var user = await GetUserEntityAsync(userId, asNoTracking: false);

        if (request.RoleId.HasValue && request.RoleId.Value != user.RoleId)
        {
            await EnsureRoleExistsAsync(request.RoleId.Value);
            user.RoleId = request.RoleId.Value;
        }

        if (request.OperatorId.HasValue)
        {
            await EnsureOperatorExistsAsync(request.OperatorId);
            user.OperatorId = request.OperatorId;
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        }

        if (request.Address != null)
        {
            user.Address = NormalizeOptional(request.Address);
        }

        if (request.AvatarUrl != null)
        {
            user.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        }

        if (request.IsActive.HasValue)
        {
            if (user.UserId == currentAdminId && !request.IsActive.Value)
            {
                throw new Exception("Admin khong the tu khoa tai khoan cua minh.");
            }

            user.IsActive = request.IsActive.Value;
        }

        if (request.IsVerified.HasValue)
        {
            user.IsVerified = request.IsVerified.Value;
        }

        if (request.IsRefreshTokenRevoked.HasValue)
        {
            user.IsRefreshTokenRevoked = request.IsRefreshTokenRevoked.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return await GetUserAsync(user.UserId);
    }

    public async Task<AdminUserDto> SetActiveAsync(Guid currentAdminId, Guid userId, bool isActive)
    {
        if (currentAdminId == userId && !isActive)
        {
            throw new Exception("Admin khong the tu khoa tai khoan cua minh.");
        }

        var user = await GetUserEntityAsync(userId, asNoTracking: false);
        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return await GetUserAsync(user.UserId);
    }

    public async Task ResetPasswordAsync(Guid userId, AdminResetUserPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw new Exception("Mat khau moi phai co it nhat 6 ky tu.");
        }

        var user = await GetUserEntityAsync(userId, asNoTracking: false);
        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.IsRefreshTokenRevoked = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();
    }

    public async Task<IReadOnlyCollection<RoleDto>> GetRolesAsync()
    {
        return await _uow.GetRepository<Role>().Entities
            .AsNoTracking()
            .OrderBy(r => r.RoleName)
            .Select(r => new RoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description
            })
            .ToListAsync();
    }

    private async Task<User> GetUserEntityAsync(Guid userId, bool asNoTracking)
    {
        IQueryable<User> query = _uow.GetRepository<User>().Entities;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        var user = await query
            .Include(u => u.Role)
            .Include(u => u.Operator)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        return user ?? throw new Exception("Khong tim thay user.");
    }

    private async Task EnsureRoleExistsAsync(int roleId)
    {
        var exists = await _uow.GetRepository<Role>().Entities
            .AsNoTracking()
            .AnyAsync(r => r.RoleId == roleId);

        if (!exists)
        {
            throw new Exception("Role khong ton tai.");
        }
    }

    private async Task EnsureOperatorExistsAsync(int? operatorId)
    {
        if (!operatorId.HasValue)
        {
            return;
        }

        var exists = await _uow.GetRepository<ServiceOperator>().Entities
            .AsNoTracking()
            .AnyAsync(o => o.OperatorId == operatorId.Value && o.IsActive);

        if (!exists)
        {
            throw new Exception("Operator khong ton tai hoac da bi khoa.");
        }
    }

    private static void ValidateCreate(AdminCreateUserRequest request)
    {
        if (request.RoleId <= 0)
        {
            throw new Exception("RoleId la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new Exception("FullName la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new Exception("Email la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new Exception("Password phai co it nhat 6 ky tu.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AdminUserDto MapUser(User user)
    {
        return new AdminUserDto
        {
            UserId = user.UserId,
            RoleId = user.RoleId,
            RoleName = user.Role.RoleName,
            OperatorId = user.OperatorId,
            OperatorName = user.Operator?.OperatorName,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            IsVerified = user.IsVerified,
            IsRefreshTokenRevoked = user.IsRefreshTokenRevoked,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
