namespace UrbanService.BLL.DTOs;

public class UserQueryParameters
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }

    public int? RoleId { get; set; }

    public string? RoleName { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsVerified { get; set; }
}

public class AdminUserDto
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; }

    public bool IsVerified { get; set; }

    public bool IsRefreshTokenRevoked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class UserProfileDto
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; }

    public bool IsVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class UpdateUserProfileRequest
{
    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }
}

public class AdminCreateUserRequest
{
    public int RoleId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsVerified { get; set; } = true;
}

public class AdminUpdateUserRequest
{
    public int? RoleId { get; set; }

    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? AvatarUrl { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsVerified { get; set; }

    public bool? IsRefreshTokenRevoked { get; set; }
}

public class AdminResetUserPasswordRequest
{
    public string NewPassword { get; set; } = null!;
}

public class AdminSetUserActiveRequest
{
    public bool IsActive { get; set; }
}

public class RoleDto
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public string? Description { get; set; }
}