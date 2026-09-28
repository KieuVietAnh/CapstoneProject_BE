using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Helpers;
using UrbanService.BLL.Common.Securities;
using UrbanService.BLL.Options;
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
        private readonly IFirebasePhoneVerifier _firebasePhoneVerifier;
        private readonly PhoneOtpOptions _phoneOtpOptions;
        private const int PasswordResetOtpMinutes = 5;
        private const int PasswordResetOtpCooldownSeconds = 60;
        private const int PasswordResetOtpMaxAttempts = 5;
        private const int DefaultRefreshTokenExpireDays = 7;
        private const string InvalidPasswordResetOtpMessage = "OTP không hợp lệ hoặc đã hết hạn.";
        private const int TooManyRequestsStatusCode = 429;
        private const int ConflictStatusCode = 409;
        private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
        private static readonly object PasswordResetCacheSync = new();

        public AuthService(
            IUnitOfWork uow,
            IConfiguration cfg,
            IJwtTokenGenerator jwt,
            IEmailSender emailSender,
            IMemoryCache cache,
            ILogger<AuthService> logger,
            IFirebasePhoneVerifier firebasePhoneVerifier,
            IOptions<PhoneOtpOptions> phoneOtpOptions)
        {
            _uow = uow;
            _cfg = cfg;
            _jwt = jwt;
            _emailSender = emailSender;
            _cache = cache;
            _logger = logger;
            _firebasePhoneVerifier = firebasePhoneVerifier;
            _phoneOtpOptions = phoneOtpOptions.Value;
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

            /*
             * Số điện thoại là bắt buộc vì OTP xác thực tài khoản gửi qua SMS. Email
             * vẫn bắt buộc nhưng để dùng cho luồng quên mật khẩu, nơi gửi mail gần
             * như không tốn gì trong khi mỗi tin SMS là chi phí thật.
             */
            if (string.IsNullOrWhiteSpace(req.Phone))
            {
                throw new Exception("Số điện thoại là bắt buộc.");
            }

            var phoneNumber = PhoneNumberHelper.NormalizeRequired(req.Phone);
            var fullName = string.IsNullOrWhiteSpace(req.Fullname) ? email : req.Fullname.Trim();

            if (req.Password.Length < PasswordPolicy.MinLength)
            {
                throw new Exception(
                    $"Mật khẩu phải có ít nhất {PasswordPolicy.MinLength} ký tự.");
            }

            var userRepo = _uow.GetRepository<User>();
            await EnsurePhoneNumberAvailableAsync(phoneNumber, excludedUserId: null);
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
                existingUser.PhoneNumber = phoneNumber;
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
                PhoneNumber = phoneNumber,
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
             * Google đăng nhập lần đầu thì tạo luôn tài khoản, nhưng IsVerified vẫn
             * là false: Google chứng minh quyền sở hữu email, không phải số điện
             * thoại, mà cờ này nay mang nghĩa đã xác thực số điện thoại. Tài khoản
             * kiểu này chưa có số nào cả, nên họ phải qua màn xác thực nhập số rồi
             * nhận OTP trước khi gửi được phản ánh.
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

        /// <summary>
        /// Bước 1 của xác thực số điện thoại: xin phép gửi SMS OTP.
        ///
        /// Firebase gửi SMS từ phía client, backend không gọi Firebase để gửi. Nhưng
        /// hạn mức phải nằm ở đây: chặn ở frontend thì bất kỳ ai gọi thẳng Firebase
        /// bằng API key công khai vẫn đốt tiền của dự án. Client chỉ được gọi
        /// Firebase sau khi endpoint này trả về thành công.
        /// </summary>
        public async Task<RequestPhoneOtpResultDto> RequestPhoneOtpAsync(
            Guid userId,
            RequestPhoneOtpRequest req,
            CancellationToken cancellationToken = default)
        {
            var user = await _uow.GetRepository<User>().GetByIdAsync(userId)
                ?? throw new Exception("Không tìm thấy người dùng.");

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Tài khoản đã bị khóa.");
            }

            if (user.IsVerified)
            {
                throw new Exception("Tài khoản đã được xác thực.");
            }

            /*
             * Cho phép đổi số ngay tại bước này: người gõ nhầm số lúc đăng ký sẽ
             * không nhận được OTP, nếu bắt họ giữ nguyên số sai thì họ kẹt hẳn.
             */
            var phoneNumber = string.IsNullOrWhiteSpace(req.PhoneNumber)
                ? PhoneNumberHelper.NormalizeRequired(user.PhoneNumber)
                : PhoneNumberHelper.NormalizeRequired(req.PhoneNumber);

            await EnsurePhoneNumberAvailableAsync(phoneNumber, excludedUserId: userId);

            if (!string.Equals(user.PhoneNumber, phoneNumber, StringComparison.Ordinal))
            {
                user.PhoneNumber = phoneNumber;
                user.UpdatedAt = DateTime.UtcNow;
                await _uow.SaveAsync();
            }

            /*
             * Số test khai trong Firebase Console dùng mã cố định và không phát sinh
             * SMS thật, nên không tính vào hạn mức. Nếu tính, cả nhóm sẽ đốt sạch
             * hạn mức chỉ bằng việc demo đi demo lại.
             */
            if (IsTestPhoneNumber(phoneNumber))
            {
                return new RequestPhoneOtpResultDto
                {
                    PhoneNumber = phoneNumber,
                    RemainingToday = null,
                    IsTestNumber = true
                };
            }

            var today = VietnamToday();
            var otpRepo = _uow.GetRepository<PhoneOtpRequest>();
            var sentToday = await otpRepo.Entities
                .AsNoTracking()
                .CountAsync(item => item.Day == today, cancellationToken);

            if (sentToday >= _phoneOtpOptions.DailyLimit)
            {
                throw new BusinessRuleException(
                    BusinessErrorCode.DailyOtpLimitReached,
                    $"Hôm nay hệ thống đã dùng hết {_phoneOtpOptions.DailyLimit} lượt gửi mã OTP. " +
                    "Vui lòng thử lại vào ngày mai.",
                    TooManyRequestsStatusCode);
            }

            await otpRepo.AddAsync(new PhoneOtpRequest
            {
                PhoneNumber = phoneNumber,
                Day = today,
                RequestedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _uow.SaveAsync();

            _logger.LogInformation(
                "Cho phép gửi OTP tới số của user {UserId} ({Count}/{Limit} hôm nay)",
                userId,
                sentToday + 1,
                _phoneOtpOptions.DailyLimit);

            return new RequestPhoneOtpResultDto
            {
                PhoneNumber = phoneNumber,
                RemainingToday = _phoneOtpOptions.DailyLimit - sentToday - 1,
                IsTestNumber = false
            };
        }

        /// <summary>
        /// Bước 2: client gửi lên Firebase ID token nhận được sau khi nhập đúng OTP.
        ///
        /// Số điện thoại lấy từ token đã qua kiểm tra chữ ký, không lấy từ request,
        /// nếu không thì client chỉ cần gửi đại một số là qua cửa.
        /// </summary>
        public async Task<AuthResultDto> VerifyPhoneAsync(
            Guid userId,
            VerifyPhoneRequest req,
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
                return await IssueAuthResultAsync(user);
            }

            var verified = await _firebasePhoneVerifier.VerifyAsync(req.IdToken, cancellationToken);
            var phoneNumber = PhoneNumberHelper.NormalizeRequired(verified.PhoneNumber);

            await EnsurePhoneNumberAvailableAsync(phoneNumber, excludedUserId: userId);

            var now = DateTime.UtcNow;
            user.PhoneNumber = phoneNumber;
            user.FirebaseUid = verified.FirebaseUid;
            user.IsVerified = true;
            user.PhoneVerifiedAt = now;
            user.UpdatedAt = now;
            await _uow.SaveAsync();

            _logger.LogInformation("Tài khoản {UserId} đã xác thực số điện thoại.", userId);

            return await IssueAuthResultAsync(user);
        }

        /// <summary>
        /// Một số điện thoại chỉ thuộc về một tài khoản đã xác thực.
        ///
        /// Không đặt unique index ở database vì dữ liệu hiện có có thể đã trùng số,
        /// mà migration lỗi thì container mới không khởi động được. Ràng buộc đặt ở
        /// tầng nghiệp vụ, nơi chỉ chặn đúng lúc một tài khoản muốn xác thực.
        /// </summary>
        private async Task EnsurePhoneNumberAvailableAsync(string phoneNumber, Guid? excludedUserId)
        {
            var taken = await _uow.GetRepository<User>().Entities
                .AsNoTracking()
                .AnyAsync(candidate =>
                    candidate.IsVerified &&
                    candidate.PhoneNumber == phoneNumber &&
                    (excludedUserId == null || candidate.UserId != excludedUserId));

            if (taken)
            {
                throw new BusinessRuleException(
                    BusinessErrorCode.PhoneAlreadyUsed,
                    "Số điện thoại này đã được dùng cho một tài khoản khác.",
                    ConflictStatusCode);
            }
        }

        private bool IsTestPhoneNumber(string phoneNumber)
        {
            return _phoneOtpOptions.TestNumbers
                .Select(PhoneNumberHelper.Normalize)
                .Any(candidate => string.Equals(candidate, phoneNumber, StringComparison.Ordinal));
        }

        /// <summary>Ngày làm việc theo giờ Việt Nam, để hạn mức reset lúc nửa đêm ở đây.</summary>
        private static string VietnamToday() =>
            DateTimeOffset.UtcNow.ToOffset(VietnamOffset).ToString("yyyy-MM-dd");

        /// <summary>
        /// Sửa thông tin đăng ký của tài khoản chưa xác thực số điện thoại.
        ///
        /// Dùng khi người dùng gõ nhầm thông tin lúc đăng ký và muốn quay lại sửa
        /// trước khi xác thực. Đăng ký lại cũng không xong vì email cũ đã chiếm chỗ.
        ///
        /// Giữ nguyên email hoặc số điện thoại của chính mình thì không báo trùng,
        /// chỉ báo khi chúng đang thuộc về tài khoản khác. Không gửi OTP ở đây:
        /// người dùng bấm gửi mã ở màn xác thực, nơi hạn mức SMS được đếm.
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
                    "Tài khoản đã xác thực nên không sửa được qua API này.");
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

            var phoneNumber = PhoneNumberHelper.NormalizeRequired(req.PhoneNumber);
            await EnsurePhoneNumberAvailableAsync(phoneNumber, excludedUserId: userId);

            user.FullName = string.IsNullOrWhiteSpace(req.FullName)
                ? email
                : req.FullName.Trim();
            user.Email = email;
            user.PhoneNumber = phoneNumber;
            user.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveAsync();

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
            string? displayName)
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
