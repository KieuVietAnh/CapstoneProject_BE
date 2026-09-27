using System.Globalization;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Common;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;

namespace UrbanService.BLL.Services;

public sealed class FirebasePhoneVerifier : IFirebasePhoneVerifier
{
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebasePhoneVerifier> _logger;

    public FirebasePhoneVerifier(
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePhoneVerifier> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VerifiedPhoneNumber> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new Exception("Thiếu Firebase ID token.");
        }

        var auth = FirebaseAuth.DefaultInstance
            ?? throw new Exception(
                "Server chưa cấu hình Firebase nên chưa xác thực được số điện thoại.");

        FirebaseToken token;
        try
        {
            /*
             * Firebase Admin SDK kiểm tra chữ ký, thời hạn và đúng project. Đây là
             * bước biến một chuỗi client gửi lên thành bằng chứng có thể tin được.
             */
            token = await auth.VerifyIdTokenAsync(idToken, cancellationToken);
        }
        catch (Exception ex) when (ex is FirebaseAuthException or ArgumentException)
        {
            _logger.LogWarning("Firebase ID token không hợp lệ: {Error}", ex.Message);
            throw new Exception("Xác thực OTP không hợp lệ hoặc đã hết hạn. Vui lòng gửi lại mã.");
        }

        if (!token.Claims.TryGetValue("phone_number", out var phoneClaim) ||
            phoneClaim is not string phoneNumber ||
            string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new Exception("Token Firebase không chứa số điện thoại đã xác thực.");
        }

        /*
         * auth_time là lúc người dùng nhập đúng OTP. Token có thể còn hạn cả giờ,
         * nhưng chỉ lần nhập mã gần đây mới chứng minh họ đang giữ chiếc SIM đó.
         */
        if (token.Claims.TryGetValue("auth_time", out var authTimeClaim))
        {
            var authTime = DateTimeOffset.FromUnixTimeSeconds(
                Convert.ToInt64(authTimeClaim, CultureInfo.InvariantCulture));

            if (DateTimeOffset.UtcNow - authTime > TimeSpan.FromMinutes(_options.MaxAuthAgeMinutes))
            {
                throw new Exception("Phiên xác thực OTP đã quá hạn. Vui lòng gửi lại mã.");
            }
        }

        return new VerifiedPhoneNumber(token.Uid, phoneNumber);
    }
}
