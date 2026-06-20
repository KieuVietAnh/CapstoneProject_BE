using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IUserManagementService
{
    Task<PagedResultDto<AdminUserDto>> GetUsersAsync(UserQueryParameters query);

    Task<AdminUserDto> GetUserAsync(Guid userId);

    Task<AdminUserDto> CreateUserAsync(AdminCreateUserRequest request);

    Task<AdminUserDto> UpdateUserAsync(Guid currentAdminId, Guid userId, AdminUpdateUserRequest request);

    Task<AdminUserDto> SetActiveAsync(Guid currentAdminId, Guid userId, bool isActive);

    Task ResetPasswordAsync(Guid userId, AdminResetUserPasswordRequest request);

    Task<IReadOnlyCollection<RoleDto>> GetRolesAsync();
}
