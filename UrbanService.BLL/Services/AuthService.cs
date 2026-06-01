using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Securities;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _uow;
        private readonly IConfiguration _cfg;
        private readonly IJwtTokenGenerator _jwt;

        public AuthService(
            IUnitOfWork uow,
            IConfiguration cfg,
            IJwtTokenGenerator jwt)
        {
            _uow = uow;
            _cfg = cfg;
            _jwt = jwt;
        }

        public async Task<AuthResultDto> LoginAsync(LoginRequest req)
        {
            var login = req.Email?.Trim();

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(req.Password))
            {
                throw new Exception("Email và mật khẩu là bắt buộc.");
            }

            var userRepo = _uow.GetRepository<User>();
            var user = await userRepo.FindAsync(
                u => u.Email.ToLower() == login.ToLower(),
                q => q.Include(u => u.Role));

            if (user == null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");
            }

            return ToAuthResult(user);
        }

        public async Task<AuthResultDto> RegisterAsync(RegisterRequest req)
        {
            var email =  req.Email.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
            {
                throw new Exception("Email và mật khẩu là bắt buộc.");
            }

            var fullName = string.IsNullOrWhiteSpace(req.Fullname) ? email : req.Fullname.Trim();

            if (req.Password.Length < 6)
            {
                throw new Exception("Mật khẩu phải có ít nhất 6 ký tự.");
            }

            var userRepo = _uow.GetRepository<User>();
            var existingUser = await userRepo.FindAsync(u => u.Email.ToLower() == email.ToLower(), include: null);

            if (existingUser != null)
            {
                throw new Exception("Email đã được sử dụng.");
            }

            var role = await GetOrCreateDefaultRoleAsync();
            var now = DateTime.UtcNow;
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = role.RoleId,
                FullName = fullName,
                Email = email,
                PasswordHash = PasswordHasher.Hash(req.Password),
                PhoneNumber = req.Phone,
                IsActive = true,
                IsRefreshTokenRevoked = false,
                CreatedAt = now,
                UpdatedAt = now,
                Role = role
            };

            await userRepo.AddAsync(user);
            await _uow.SaveAsync();

            return ToAuthResult(user);
        }

        private async Task<Role> GetOrCreateDefaultRoleAsync()
        {
            var defaultRole = _cfg["Auth:DefaultRole"] ?? UserRole.SERVICEUSER;
            var roleRepo = _uow.GetRepository<Role>();
            var role = await roleRepo.FindAsync(r => r.RoleName.ToUpper() == defaultRole.ToUpper(), include: null);

            if (role != null)
            {
                return role;
            }

            role = new Role
            {
                RoleName = defaultRole,
                Description = "Default registered user role"
            };

            await roleRepo.AddAsync(role);
            await _uow.SaveAsync();

            return role;
        }

        private AuthResultDto ToAuthResult(User user)
        {
            return new AuthResultDto
            {
                Token = _jwt.Generate(user),
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role?.RoleName
            };
        }
    }
}
