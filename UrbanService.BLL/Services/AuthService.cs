using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Securities;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;
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
        private readonly ISmsSender _smsSender;
        private readonly TwilioOptions _twilioOptions;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthService> _logger;
        private const int VerificationOtpMinutes = 5;
        private const int VerificationOtpCooldownSeconds = 60;
        private const int VerificationOtpMaxAttempts = 5;
        private const string InvalidPhoneOtpMessage = "OTP không đúng hoặc đã hết hạn.";
        private static readonly object PhoneVerificationCacheSync = new();
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
            ISmsSender smsSender,
            IOptions<TwilioOptions> twilioOptions,
            IMemoryCache cache,
            ILogger<AuthService> logger)
        {
            _uow = uow;
            _cfg = cfg;
            _jwt = jwt;
            _emailSender = emailSender;
            _smsSender = smsSender;
            _twilioOptions = twilioOptions.Value;
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

        public async Task<AuthResultDto> RegisterAsync(
            RegisterRequest req,
            CancellationToken cancellationToken = default)
        {
            var email = NormalizeEmail(req.Email);

            if (string.IsNullOrWhiteSpace(req.Password))
            {
                throw new Exception("Mật khẩu là bắt buộc.");
            }

            if (req.Password.Length < 6)
            {
                throw new Exception("Mật khẩu phải có ít nhất 6 ký tự.");
            }

            var phoneNumber = NormalizePhoneNumber(req.Phone);
            var fullName = string.IsNullOrWhiteSpace(req.Fullname) ? email : req.Fullname.Trim();

            var userRepo = _uow.GetRepository<User>();
            var existingUser = await userRepo.FindAsync(u => u.Email.ToLower() == email, include: null);

            if (existingUser != null)
            {
                /*
                 * Tài khoản đã tồn tại nhưng chưa xác thực thì cho đăng ký lại:
                 * cập nhật thông tin mới và gửi lại OTP. Nếu không, người dùng
                 * lỡ mất OTP sẽ bị kẹt vĩnh viễn với một email không dùng được.
                 */
                if (existingUser.IsVerified)
                {
                    throw new Exception("Email đã được sử dụng.");
                }

                existingUser.FullName = fullName;
                existingUser.PasswordHash = PasswordHasher.Hash(req.Password);
                existingUser.PhoneNumber = phoneNumber;
                existingUser.UpdatedAt = DateTime.UtcNow;
                await _uow.SaveAsync();

                await SendPhoneVerificationOtpAsync(existingUser, phoneNumber, cancellationToken);

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
                PhoneNumber = phoneNumber,
                IsActive = true,
                IsVerified = false,
                IsRefreshTokenRevoked = false,
                CreatedAt = now,
                UpdatedAt = now,
                Role = role
            };

            await userRepo.AddAsync(user);
            await _uow.SaveAsync();

            await SendPhoneVerificationOtpAsync(user, phoneNumber, cancellationToken);

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
             * Google đăng nhập lần đầu thì tạo luôn tài khoản. Email đã được
             * Google xác thực nên không cần OTP email; tài khoản vào được hệ
             * thống ngay với IsVerified = false và sẽ bị chặn ở bước gửi phản
             * ánh cho tới khi bổ sung và xác thực số điện thoại.
             */
            if (user == null)
            {
                user = await CreateGoogleUserAsync(email, payload.Name);
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

        public async Task RequestPhoneVerificationOtpAsync(
            SendPhoneOtpRequest req,
            CancellationToken cancellationToken = default)
        {
            var phoneNumber = NormalizePhoneNumber(req.Phone);

            var user = await _uow.GetRepository<User>().Entities
                .FirstOrDefaultAsync(
                    candidate => candidate.IsActive && candidate.PhoneNumber == phoneNumber,
                    cancellationToken);

            /*
             * Không tiết lộ số nào đã đăng ký: trả về im lặng khi không tìm thấy
             * tài khoản hoặc tài khoản đã xác thực.
             */
            if (user == null || user.IsVerified)
            {
                return;
            }

            await SendPhoneVerificationOtpAsync(user, phoneNumber, cancellationToken);
        }

        /// <summary>
        /// Sinh OTP, lưu bản băm vào cache rồi gửi SMS.
        ///
        /// OTP chỉ lưu dạng băm để log hay dump cache cũng không lộ mã. Nếu gửi
        /// SMS thất bại thì gỡ luôn OTP và cooldown, để người dùng thử lại ngay
        /// thay vì bị khóa chờ vô ích.
        /// </summary>
        private async Task SendPhoneVerificationOtpAsync(
            User user,
            string phoneNumber,
            CancellationToken cancellationToken)
        {
            var otpKey = GetPhoneOtpKey(phoneNumber);
            var cooldownKey = GetPhoneOtpCooldownKey(phoneNumber);

            lock (PhoneVerificationCacheSync)
            {
                if (_cache.TryGetValue(cooldownKey, out _))
                {
                    throw new Exception(
                        $"Vui lòng chờ {VerificationOtpCooldownSeconds} giây trước khi gửi lại OTP.");
                }
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var state = new PhoneVerificationOtpState
            {
                UserId = user.UserId,
                OtpHash = PasswordHasher.Hash(otp)
            };

            lock (PhoneVerificationCacheSync)
            {
                _cache.Set(cooldownKey, true, TimeSpan.FromSeconds(VerificationOtpCooldownSeconds));
                _cache.Set(otpKey, state, TimeSpan.FromMinutes(VerificationOtpMinutes));
            }

            try
            {
                await _smsSender.SendAsync(
                    phoneNumber,
                    $"UrbanService: ma OTP xac thuc cua ban la {otp}. " +
                    $"Ma co hieu luc trong {VerificationOtpMinutes} phut. Khong chia se ma nay voi ai.",
                    cancellationToken);
            }
            catch (Exception)
            {
                RemovePhoneOtpIssuance(otpKey, cooldownKey, state);
                throw;
            }
        }

        public async Task<AuthResultDto> VerifyPhoneAsync(
            VerifyPhoneRequest req,
            CancellationToken cancellationToken = default)
        {
            var phoneNumber = NormalizePhoneNumber(req.Phone);
            var otp = req.Otp?.Trim();

            if (string.IsNullOrWhiteSpace(otp) || otp.Length != 6 || !otp.All(char.IsDigit))
            {
                throw new Exception(InvalidPhoneOtpMessage);
            }

            var otpKey = GetPhoneOtpKey(phoneNumber);
            if (!_cache.TryGetValue<PhoneVerificationOtpState>(otpKey, out var state) || state == null)
            {
                throw new Exception(InvalidPhoneOtpMessage);
            }

            var user = await _uow.GetRepository<User>().FindAsync(
                candidate => candidate.UserId == state.UserId,
                q => q.Include(candidate => candidate.Role));

            if (user == null || !user.IsActive || user.PhoneNumber != phoneNumber)
            {
                throw new Exception(InvalidPhoneOtpMessage);
            }

            lock (state.SyncRoot)
            {
                if (!_cache.TryGetValue<PhoneVerificationOtpState>(otpKey, out var currentState) ||
                    !ReferenceEquals(currentState, state))
                {
                    throw new Exception(InvalidPhoneOtpMessage);
                }

                if (!PasswordHasher.Verify(otp, state.OtpHash))
                {
                    /*
                     * Đếm số lần sai để chặn dò OTP. Hết lượt thì hủy mã, người
                     * dùng phải yêu cầu gửi lại.
                     */
                    state.FailedAttempts++;
                    if (state.FailedAttempts >= VerificationOtpMaxAttempts)
                    {
                        _cache.Remove(otpKey);
                    }

                    throw new Exception(InvalidPhoneOtpMessage);
                }
            }

            if (!user.IsVerified)
            {
                user.IsVerified = true;
                user.UpdatedAt = DateTime.UtcNow;
            }

            var result = await IssueAuthResultAsync(user);

            _cache.Remove(otpKey);
            _cache.Remove(GetPhoneOtpCooldownKey(phoneNumber));

            return result;
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

        public async Task ResetPasswordAsync(
            ResetPasswordRequest req,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = NormalizeEmail(req.Email);

            if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            {
                throw new Exception("Mật khẩu mới phải có ít nhất 6 ký tự.");
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
        private async Task<User> CreateGoogleUserAsync(string email, string? displayName)
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
                IsVerified = false,
                IsRefreshTokenRevoked = false,
                CreatedAt = now,
                UpdatedAt = now,
                Role = role
            };

            await _uow.GetRepository<User>().AddAsync(user);
            await _uow.SaveAsync();

            return user;
        }

        public async Task AttachPhoneAsync(
            Guid userId,
            SendPhoneOtpRequest req,
            CancellationToken cancellationToken = default)
        {
            var phoneNumber = NormalizePhoneNumber(req.Phone);

            var user = await _uow.GetRepository<User>().GetByIdAsync(userId)
                ?? throw new Exception("Không tìm thấy người dùng.");

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");
            }

            /*
             * Đã xác thực rồi thì không cho đổi số qua đường này. Đổi số điện
             * thoại của tài khoản đã xác thực là nghiệp vụ khác, cần luồng riêng
             * để không biến đây thành cách chiếm tài khoản.
             */
            if (user.IsVerified)
            {
                throw new Exception("Tài khoản đã xác thực số điện thoại.");
            }

            user.PhoneNumber = phoneNumber;
            user.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveAsync();

            await SendPhoneVerificationOtpAsync(user, phoneNumber, cancellationToken);
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

        private static string GetPhoneOtpKey(string phoneNumber) =>
            $"phone-verification:{HashCacheSubject(phoneNumber)}";

        private static string GetPhoneOtpCooldownKey(string phoneNumber) =>
            $"phone-verification-cooldown:{HashCacheSubject(phoneNumber)}";

        private void RemovePhoneOtpIssuance(
            string otpKey,
            string cooldownKey,
            PhoneVerificationOtpState state)
        {
            lock (PhoneVerificationCacheSync)
            {
                if (_cache.TryGetValue<PhoneVerificationOtpState>(otpKey, out var currentState) &&
                    ReferenceEquals(currentState, state))
                {
                    _cache.Remove(otpKey);
                    _cache.Remove(cooldownKey);
                }
            }
        }

        /// <summary>
        /// Chuẩn hóa số điện thoại về dạng E.164 để Twilio nhận.
        ///
        /// Chấp nhận số đã có mã quốc gia (+84..., 84...) và số nội địa bắt đầu
        /// bằng 0. Ký tự phân cách như khoảng trắng, dấu chấm, gạch ngang và
        /// ngoặc đơn được bỏ đi trước khi kiểm tra.
        /// </summary>
        private string NormalizePhoneNumber(string? phone)
        {
            const string invalidMessage = "Số điện thoại không hợp lệ.";

            if (string.IsNullOrWhiteSpace(phone))
            {
                throw new Exception("Số điện thoại là bắt buộc.");
            }

            var hasPlusPrefix = phone.TrimStart().StartsWith('+');
            var digits = Regex.Replace(phone, @"[^0-9]", string.Empty);

            if (digits.Length == 0)
            {
                throw new Exception(invalidMessage);
            }

            var countryCode = string.IsNullOrWhiteSpace(_twilioOptions.DefaultCountryCode)
                ? "+84"
                : _twilioOptions.DefaultCountryCode.Trim();
            var countryDigits = Regex.Replace(countryCode, @"[^0-9]", string.Empty);

            string e164;
            if (hasPlusPrefix)
            {
                e164 = $"+{digits}";
            }
            else if (digits.StartsWith('0'))
            {
                e164 = $"+{countryDigits}{digits.TrimStart('0')}";
            }
            else if (countryDigits.Length > 0 && digits.StartsWith(countryDigits, StringComparison.Ordinal))
            {
                e164 = $"+{digits}";
            }
            else
            {
                e164 = $"+{countryDigits}{digits}";
            }

            /*
             * E.164 cho phép tối đa 15 chữ số kể cả mã quốc gia, tối thiểu 8 chữ
             * số là ngưỡng thực tế để loại các số rõ ràng sai.
             */
            var normalizedDigits = e164[1..];
            if (normalizedDigits.Length < 8 || normalizedDigits.Length > 15)
            {
                throw new Exception(invalidMessage);
            }

            return e164;
        }

        /// <summary>
        /// Che bớt số điện thoại khi trả về cho client, chỉ giữ mã quốc gia và
        /// 3 chữ số cuối để người dùng nhận ra số của mình.
        /// </summary>
        private static string MaskPhoneNumber(string e164PhoneNumber)
        {
            if (e164PhoneNumber.Length <= 5)
            {
                return e164PhoneNumber;
            }

            var visibleTail = e164PhoneNumber[^3..];
            var hiddenLength = e164PhoneNumber.Length - 3 - 3;

            return hiddenLength <= 0
                ? e164PhoneNumber
                : $"{e164PhoneNumber[..3]}{new string('*', hiddenLength)}{visibleTail}";
        }

        private sealed class PhoneVerificationOtpState
        {
            public Guid UserId { get; init; }

            public string OtpHash { get; init; } = null!;

            public int FailedAttempts { get; set; }

            public object SyncRoot { get; } = new();
        }

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
