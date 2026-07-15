using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
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
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _cache;
        private const int VerificationOtpMinutes = 5;
        private const int VerificationOtpCooldownSeconds = 60;
        private const int DefaultRefreshTokenExpireDays = 7;

        public AuthService(
            IUnitOfWork uow,
            IConfiguration cfg,
            IJwtTokenGenerator jwt,
            IEmailSender emailSender,
            IMemoryCache cache)
        {
            _uow = uow;
            _cfg = cfg;
            _jwt = jwt;
            _emailSender = emailSender;
            _cache = cache;
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

            return await IssueAuthResultAsync(user);
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
                IsVerified = false,
                IsRefreshTokenRevoked = false,
                CreatedAt = now,
                UpdatedAt = now,
                Role = role
            };

            await userRepo.AddAsync(user);

            return await IssueAuthResultAsync(user);
        }

        public async Task<AuthResultDto> GoogleLoginAsync(GoogleLoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.IdToken))
            {
                throw new Exception("Google ID token là bắt buộc.");
            }

            var clientId = _cfg["GoogleAuth:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException("Missing config: GoogleAuth:ClientId");
            }

            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(
                    req.IdToken.Trim(),
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = [clientId]
                    });
            }
            catch (InvalidJwtException)
            {
                throw new UnauthorizedAccessException("Google ID token không hợp lệ hoặc đã hết hạn.");
            }

            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
            {
                throw new UnauthorizedAccessException("Google chưa xác thực email này.");
            }

            var email = payload.Email.Trim().ToLower();
            var user = await _uow.GetRepository<User>().FindAsync(
                u => u.Email.ToLower() == email,
                q => q.Include(u => u.Role));

            if (user == null)
            {
                throw new UnauthorizedAccessException("Tài khoản chưa tồn tại trong UrbanService.");
            }

            if (!user.IsVerified)
            {
                throw new UnauthorizedAccessException("Tài khoản UrbanService chưa xác thực email.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");
            }

            return await IssueAuthResultAsync(user);
        }

        public async Task<AuthResultDto> RefreshTokenAsync(RefreshTokenRequest req)
        {
            var refreshToken = req.RefreshToken?.Trim();

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new UnauthorizedAccessException();
            }

            if (!TryGetRefreshTokenExpiresAt(refreshToken, out var expiresAt))
            {
                throw new UnauthorizedAccessException();
            }

            var refreshTokenHash = HashRefreshToken(refreshToken);
            var user = await _uow.GetRepository<User>().FindAsync(
                u => u.RefreshToken == refreshTokenHash,
                q => q.Include(u => u.Role));

            if (user == null || !user.IsActive || user.IsRefreshTokenRevoked)
            {
                throw new UnauthorizedAccessException();
            }

            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                user.IsRefreshTokenRevoked = true;
                user.UpdatedAt = DateTime.UtcNow;
                await _uow.SaveAsync();
                throw new UnauthorizedAccessException();
            }

            return await IssueAuthResultAsync(user);
        }

        public async Task RequestEmailVerificationOtpAsync(Guid userId)
        {
            var user = await _uow.GetRepository<User>().GetByIdAsync(userId)
                ?? throw new Exception("Không tìm thấy người dùng.");

            if (user.IsVerified)
            {
                throw new Exception("Email đã được xác thực.");
            }

            if (_cache.TryGetValue(GetVerificationOtpCooldownKey(userId), out _))
            {
                throw new Exception($"Vui lòng chờ {VerificationOtpCooldownSeconds} giây trước khi gửi lại OTP.");
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var body = $"""
                <h2>Xác thực email UrbanService</h2>
                <p>Xin chào {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
                <p>Mã OTP xác thực email của bạn là:</p>
                <h1 style="letter-spacing: 6px">{otp}</h1>
                <p>Mã có hiệu lực trong {VerificationOtpMinutes} phút.</p>
                """;

            await _emailSender.SendAsync(new EmailMessageDto
            {
                To = [user.Email],
                Subject = "Mã OTP xác thực email UrbanService",
                Body = body
            });
            _cache.Set(GetVerificationOtpKey(userId), otp, TimeSpan.FromMinutes(VerificationOtpMinutes));
            _cache.Set(
                GetVerificationOtpCooldownKey(userId),
                true,
                TimeSpan.FromSeconds(VerificationOtpCooldownSeconds));
        }

        public async Task VerifyEmailAsync(Guid userId, VerifyEmailRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Otp))
            {
                throw new Exception("OTP là bắt buộc.");
            }

            var user = await _uow.GetRepository<User>().GetByIdAsync(userId)
                ?? throw new Exception("Không tìm thấy người dùng.");

            if (user.IsVerified)
            {
                return;
            }

            if (!_cache.TryGetValue<string>(GetVerificationOtpKey(userId), out var otp) ||
                !string.Equals(otp, req.Otp.Trim(), StringComparison.Ordinal))
            {
                throw new Exception("OTP không đúng hoặc đã hết hạn.");
            }

            user.IsVerified = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveAsync();
            _cache.Remove(GetVerificationOtpKey(userId));
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

        private async Task<AuthResultDto> IssueAuthResultAsync(User user)
        {
            var (refreshToken, _) = GenerateRefreshToken();
            user.RefreshToken = HashRefreshToken(refreshToken);
            user.IsRefreshTokenRevoked = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _uow.SaveAsync();

            return ToAuthResult(user, refreshToken);
        }

        private AuthResultDto ToAuthResult(User user, string refreshToken)
        {
            return new AuthResultDto
            {
                Token = _jwt.Generate(user),
                RefreshToken = refreshToken,
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role?.RoleName,
                IsVerified = user.IsVerified
            };
        }

        private (string Token, DateTimeOffset ExpiresAt) GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            var token = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            var expiresAt = DateTimeOffset.UtcNow.AddDays(GetRefreshTokenExpireDays());

            return ($"{token}.{expiresAt.ToUnixTimeSeconds()}", expiresAt);
        }

        private int GetRefreshTokenExpireDays()
        {
            return int.TryParse(_cfg["Jwt:RefreshTokenExpireDays"], out var days) && days > 0
                ? days
                : DefaultRefreshTokenExpireDays;
        }

        private static string HashRefreshToken(string refreshToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToHexString(bytes);
        }

        private static bool TryGetRefreshTokenExpiresAt(string refreshToken, out DateTimeOffset expiresAt)
        {
            expiresAt = default;
            var separatorIndex = refreshToken.LastIndexOf('.');

            if (separatorIndex < 0 || separatorIndex == refreshToken.Length - 1)
            {
                return false;
            }

            var expiresAtText = refreshToken[(separatorIndex + 1)..];

            if (!long.TryParse(expiresAtText, out var unixSeconds))
            {
                return false;
            }

            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }

        private static string GetVerificationOtpKey(Guid userId) => $"email-verification:{userId}";

        private static string GetVerificationOtpCooldownKey(Guid userId) =>
            $"email-verification-cooldown:{userId}";
    }
}
