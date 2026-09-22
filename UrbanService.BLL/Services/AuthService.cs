using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
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
        private readonly ILogger<AuthService> _logger;
        private const int VerificationOtpMinutes = 5;
        private const int VerificationOtpCooldownSeconds = 60;
        private const int PasswordResetOtpMinutes = 5;
        private const int PasswordResetOtpCooldownSeconds = 60;
        private const int PasswordResetOtpMaxAttempts = 5;
        private const int DefaultRefreshTokenExpireDays = 7;
        private const string InvalidPasswordResetOtpMessage = "OTP không hợp lệ hoặc đã hết hạn.";
        private static readonly object PasswordResetCacheSync = new();

        public AuthService(
            IUnitOfWork uow,
            IConfiguration cfg,
            IJwtTokenGenerator jwt,
            IEmailSender emailSender,
            IMemoryCache cache,
            ILogger<AuthService> logger)
        {
            _uow = uow;
            _cfg = cfg;
            _jwt = jwt;
            _emailSender = emailSender;
            _cache = cache;
            _logger = logger;
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

            /*
             * Không chặn tài khoản chưa xác thực SĐT ở đây. Đăng nhập là tự do;
             * xác thực SĐT chỉ là điều kiện để gửi phản ánh, và chốt đó nằm ở
             * FeedbackService. Client đọc AuthResultDto.IsVerified để biết có
             * cần nhắc người dùng xác thực hay không.
             */
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

            if (req.Password.Length < PasswordPolicy.MinLength)
            {
                throw new Exception(
                    $"Mật khẩu phải có ít nhất {PasswordPolicy.MinLength} ký tự.");
            }

            var userRepo = _uow.GetRepository<User>();
            var existingUser = await userRepo.FindAsync(
                u => u.Email.ToLower() == email.ToLower(),
                q => q.Include(u => u.Role));

            if (existingUser != null)
            {
                /*
                 * Tài khoản đã xác thực thì email coi như có chủ, không cho ghi đè.
                 * Tài khoản bị khóa cũng vậy: cho đăng ký lại sẽ thành đường mở lại
                 * tài khoản đã bị chặn.
                 */
                if (existingUser.IsVerified || !existingUser.IsActive)
                {
                    throw new Exception("Email đã được sử dụng.");
                }

                /*
                 * Chưa xác thực thì cho đăng ký lại đè lên, vì chưa ai chứng minh
                 * quyền sở hữu email này. Không cho thì người bỏ dở giữa chừng sẽ
                 * kẹt vĩnh viễn: họ không nhận được OTP để xác thực, mà email đã bị
                 * chính tài khoản dở dang của họ chiếm chỗ.
                 */
                existingUser.FullName = fullName;
                existingUser.PasswordHash = PasswordHasher.Hash(req.Password);
                existingUser.PhoneNumber = req.Phone;
                existingUser.UpdatedAt = DateTime.UtcNow;
                await _uow.SaveAsync();

                return await IssueAuthResultAsync(existingUser);
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

            /*
             * Google đăng nhập lần đầu thì tạo luôn tài khoản. Xác thực của hệ
             * thống là xác thực email, mà Google đã xác thực email rồi, nên lấy
             * luôn payload.EmailVerified thay vì bắt người dùng nhập OTP cho
             * chính email Google vừa chứng minh quyền sở hữu.
             */
            if (user == null)
            {
                user = await CreateGoogleUserAsync(email, payload.Name, payload.EmailVerified);
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

            await SendEmailVerificationOtpAsync(user);
        }

        /// <summary>
        /// Sinh OTP mới, gửi tới email hiện tại của tài khoản và đặt lại cooldown.
        ///
        /// Tách riêng khỏi <see cref="RequestEmailVerificationOtpAsync"/> vì luồng
        /// đổi email của tài khoản chưa xác thực cũng cần gửi OTP, nhưng không được
        /// vướng cooldown của email cũ: người dùng vừa nhận mã ở địa chỉ sai thì
        /// không có lý do gì bắt họ chờ thêm một phút mới nhận được mã ở địa chỉ đúng.
        /// </summary>
        private async Task SendEmailVerificationOtpAsync(User user)
        {
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
            _cache.Set(
                GetVerificationOtpKey(user.UserId),
                otp,
                TimeSpan.FromMinutes(VerificationOtpMinutes));
            _cache.Set(
                GetVerificationOtpCooldownKey(user.UserId),
                true,
                TimeSpan.FromSeconds(VerificationOtpCooldownSeconds));
        }

        /// <summary>
        /// Sửa thông tin đăng ký của tài khoản chưa xác thực email.
        ///
        /// Dùng khi người dùng gõ nhầm email lúc đăng ký: họ không nhận được OTP
        /// nên không tự xác thực được, mà đăng ký lại cũng không xong vì email cũ
        /// đã chiếm chỗ.
        ///
        /// Giữ nguyên email của chính mình thì không báo trùng, chỉ báo khi email
        /// mới đang thuộc về tài khoản khác. Đổi email thì OTP cũ bị hủy ngay: mã
        /// đó được gửi tới hòm thư cũ, để nó còn hiệu lực nghĩa là người kiểm soát
        /// địa chỉ cũ vẫn xác thực được địa chỉ mới.
        /// </summary>
        public async Task<AuthResultDto> UpdatePendingAccountAsync(
            Guid userId,
            PendingAccountUpdateRequest req,
            CancellationToken cancellationToken = default)
        {
            var userRepo = _uow.GetRepository<User>();
            var user = await userRepo.Entities
                .Include(candidate => candidate.Role)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
                ?? throw new Exception("Không tìm thấy người dùng.");

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");
            }

            if (user.IsVerified)
            {
                throw new Exception(
                    "Tài khoản đã xác thực email nên không sửa được qua API này.");
            }

            var email = NormalizeEmail(req.Email);
            var emailChanged = !string.Equals(
                user.Email,
                email,
                StringComparison.OrdinalIgnoreCase);

            if (emailChanged)
            {
                var emailTaken = await userRepo.Entities
                    .AnyAsync(
                        candidate => candidate.UserId != userId &&
                            candidate.Email.ToLower() == email,
                        cancellationToken);

                if (emailTaken)
                {
                    throw new Exception("Email đã được sử dụng.");
                }
            }

            if (!string.IsNullOrWhiteSpace(req.NewPassword))
            {
                if (req.NewPassword.Length < PasswordPolicy.MinLength)
                {
                    throw new Exception(
                        $"Mật khẩu phải có ít nhất {PasswordPolicy.MinLength} ký tự.");
                }

                user.PasswordHash = PasswordHasher.Hash(req.NewPassword);
            }

            user.FullName = string.IsNullOrWhiteSpace(req.FullName)
                ? email
                : req.FullName.Trim();
            user.Email = email;
            user.PhoneNumber = string.IsNullOrWhiteSpace(req.PhoneNumber)
                ? null
                : req.PhoneNumber.Trim();
            user.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveAsync();

            if (emailChanged)
            {
                _cache.Remove(GetVerificationOtpKey(userId));
                _cache.Remove(GetVerificationOtpCooldownKey(userId));
                await SendEmailVerificationOtpAsync(user);
            }

            return await IssueAuthResultAsync(user);
        }

        public async Task RequestForgotPasswordOtpAsync(
            ForgotPasswordRequest req,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(req.Email);
            var user = await _uow.GetRepository<User>().Entities
                .FirstOrDefaultAsync(
                    candidate => candidate.IsActive && candidate.Email.ToLower() == normalizedEmail,
                    cancellationToken);

            if (user == null)
            {
                return;
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var otpKey = GetPasswordResetOtpKey(normalizedEmail);
            var cooldownKey = GetPasswordResetOtpCooldownKey(normalizedEmail);
            var state = new PasswordResetOtpState
            {
                UserId = user.UserId,
                OtpHash = PasswordHasher.Hash(otp)
            };

            lock (PasswordResetCacheSync)
            {
                if (_cache.TryGetValue(cooldownKey, out _))
                {
                    return;
                }

                if (_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var currentState) &&
                    currentState != null)
                {
                    lock (currentState.SyncRoot)
                    {
                        if (currentState.IsConsuming)
                        {
                            return;
                        }
                    }
                }

                _cache.Set(
                    cooldownKey,
                    true,
                    TimeSpan.FromSeconds(PasswordResetOtpCooldownSeconds));
                _cache.Set(
                    otpKey,
                    state,
                    TimeSpan.FromMinutes(PasswordResetOtpMinutes));
            }

            var body = $"""
                <h2>Đặt lại mật khẩu UrbanService</h2>
                <p>Xin chào {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
                <p>Mã OTP đặt lại mật khẩu của bạn là:</p>
                <h1 style="letter-spacing: 6px">{otp}</h1>
                <p>Mã có hiệu lực trong {PasswordResetOtpMinutes} phút.</p>
                <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                """;

            try
            {
                await _emailSender.SendAsync(new EmailMessageDto
                {
                    To = [user.Email],
                    Subject = "Mã OTP đặt lại mật khẩu UrbanService",
                    Body = body
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                RemovePasswordResetIssuance(otpKey, cooldownKey, state);
                throw;
            }
            catch (Exception)
            {
                RemovePasswordResetIssuance(otpKey, cooldownKey, state);
                _logger.LogWarning("Không thể gửi OTP đặt lại mật khẩu do lỗi nhà cung cấp email.");
            }
        }

        /// <summary>
        /// Kiểm tra OTP quên mật khẩu mà không tiêu thụ nó.
        ///
        /// Dùng cho giao diện tách làm nhiều bước: nhập email, nhập OTP, rồi mới
        /// nhập mật khẩu mới. OTP phải còn nguyên sau bước này để
        /// <see cref="ResetPasswordAsync"/> còn dùng được, nên ở đây chỉ đối chiếu
        /// chứ không xóa khỏi cache.
        ///
        /// Nhập sai vẫn cộng vào bộ đếm và vẫn hủy OTP khi chạm ngưỡng, nếu không
        /// endpoint này sẽ thành đường dò mã không giới hạn, đi vòng qua giới hạn
        /// mà luồng reset đang có.
        /// </summary>
        public async Task VerifyForgotPasswordOtpAsync(
            VerifyForgotPasswordOtpRequest req,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(req.Email);

            var otp = req.Otp?.Trim();
            if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6 || !otp.All(char.IsDigit))
            {
                throw new Exception(InvalidPasswordResetOtpMessage);
            }

            var otpKey = GetPasswordResetOtpKey(normalizedEmail);
            if (!_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var state) || state == null)
            {
                throw new Exception(InvalidPasswordResetOtpMessage);
            }

            var user = await _uow.GetRepository<User>().Entities
                .FirstOrDefaultAsync(
                    candidate => candidate.IsActive && candidate.Email.ToLower() == normalizedEmail,
                    cancellationToken);

            if (user == null || user.UserId != state.UserId)
            {
                throw new Exception(InvalidPasswordResetOtpMessage);
            }

            lock (state.SyncRoot)
            {
                if (!_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var currentState) ||
                    !ReferenceEquals(currentState, state) ||
                    state.IsConsuming)
                {
                    throw new Exception(InvalidPasswordResetOtpMessage);
                }

                if (!PasswordHasher.Verify(otp, state.OtpHash))
                {
                    state.FailedAttempts++;
                    if (state.FailedAttempts >= PasswordResetOtpMaxAttempts)
                    {
                        _cache.Remove(otpKey);
                    }

                    throw new Exception(InvalidPasswordResetOtpMessage);
                }
            }
        }

        public async Task ResetPasswordAsync(
            ResetPasswordRequest req,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(req.Email);

            if (string.IsNullOrWhiteSpace(req.NewPassword) ||
                req.NewPassword.Length < PasswordPolicy.MinLength)
            {
                throw new Exception(
                    $"Mật khẩu mới phải có ít nhất {PasswordPolicy.MinLength} ký tự.");
            }

            var otp = req.Otp?.Trim();
            if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6 || !otp.All(char.IsDigit))
            {
                throw new Exception(InvalidPasswordResetOtpMessage);
            }

            var otpKey = GetPasswordResetOtpKey(normalizedEmail);
            if (!_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var state) || state == null)
            {
                throw new Exception(InvalidPasswordResetOtpMessage);
            }

            var user = await _uow.GetRepository<User>().Entities
                .FirstOrDefaultAsync(
                    candidate => candidate.IsActive && candidate.Email.ToLower() == normalizedEmail,
                    cancellationToken);

            if (user == null || user.UserId != state.UserId)
            {
                throw new Exception(InvalidPasswordResetOtpMessage);
            }

            lock (state.SyncRoot)
            {
                if (!_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var currentState) ||
                    !ReferenceEquals(currentState, state) ||
                    state.IsConsuming)
                {
                    throw new Exception(InvalidPasswordResetOtpMessage);
                }

                if (!PasswordHasher.Verify(otp, state.OtpHash))
                {
                    state.FailedAttempts++;
                    if (state.FailedAttempts >= PasswordResetOtpMaxAttempts)
                    {
                        _cache.Remove(otpKey);
                    }

                    throw new Exception(InvalidPasswordResetOtpMessage);
                }

                state.IsConsuming = true;
            }

            var originalPasswordHash = user.PasswordHash;
            var originalRefreshToken = user.RefreshToken;
            var originalIsRefreshTokenRevoked = user.IsRefreshTokenRevoked;
            var originalUpdatedAt = user.UpdatedAt;

            try
            {
                user.PasswordHash = PasswordHasher.Hash(req.NewPassword);
                user.RefreshToken = null;
                user.IsRefreshTokenRevoked = true;
                user.UpdatedAt = DateTime.UtcNow;
                await _uow.SaveAsync();

                lock (state.SyncRoot)
                {
                    if (_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var currentState) &&
                        ReferenceEquals(currentState, state))
                    {
                        _cache.Remove(otpKey);
                    }
                }
            }
            catch
            {
                user.PasswordHash = originalPasswordHash;
                user.RefreshToken = originalRefreshToken;
                user.IsRefreshTokenRevoked = originalIsRefreshTokenRevoked;
                user.UpdatedAt = originalUpdatedAt;

                lock (state.SyncRoot)
                {
                    if (_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var currentState) &&
                        ReferenceEquals(currentState, state))
                    {
                        state.IsConsuming = false;
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Tạo tài khoản cho người dùng đăng nhập Google lần đầu.
        ///
        /// Tài khoản chưa có số điện thoại và IsVerified = false. Mật khẩu được
        /// đặt bằng một chuỗi ngẫu nhiên không ai biết, nên đường đăng nhập bằng
        /// mật khẩu coi như bị khóa; người dùng muốn dùng mật khẩu thì phải đi
        /// qua luồng quên mật khẩu.
        /// </summary>
        private async Task<User> CreateGoogleUserAsync(
            string email,
            string? displayName,
            bool emailVerified)
        {
            var role = await GetOrCreateDefaultRoleAsync();
            var now = DateTime.UtcNow;
            var user = new User
            {
                UserId = Guid.NewGuid(),
                RoleId = role.RoleId,
                FullName = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim(),
                Email = email,
                PasswordHash = PasswordHasher.Hash(
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                PhoneNumber = null,
                IsActive = true,
                IsVerified = emailVerified,
                IsRefreshTokenRevoked = false,
                CreatedAt = now,
                UpdatedAt = now,
                Role = role
            };

            await _uow.GetRepository<User>().AddAsync(user);
            await _uow.SaveAsync();

            return user;
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
                PhoneNumber = user.PhoneNumber,
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

        private static string NormalizeEmail(string? email)
        {
            var normalizedEmail = email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail) ||
                !MailAddress.TryCreate(normalizedEmail, out var parsedEmail) ||
                !string.Equals(parsedEmail.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Email không hợp lệ.");
            }

            return normalizedEmail;
        }

        private static string GetPasswordResetOtpKey(string normalizedEmail) =>
            $"password-reset:{HashCacheSubject(normalizedEmail)}";

        private static string GetPasswordResetOtpCooldownKey(string normalizedEmail) =>
            $"password-reset-cooldown:{HashCacheSubject(normalizedEmail)}";

        private static string HashCacheSubject(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private void RemovePasswordResetIssuance(
            string otpKey,
            string cooldownKey,
            PasswordResetOtpState state)
        {
            lock (PasswordResetCacheSync)
            {
                if (_cache.TryGetValue<PasswordResetOtpState>(otpKey, out var currentState) &&
                    ReferenceEquals(currentState, state))
                {
                    _cache.Remove(otpKey);
                    _cache.Remove(cooldownKey);
                }
            }
        }

        private sealed class PasswordResetOtpState
        {
            public Guid UserId { get; init; }

            public string OtpHash { get; init; } = null!;

            public int FailedAttempts { get; set; }

            public bool IsConsuming { get; set; }

            public object SyncRoot { get; } = new();
        }
    }
}
