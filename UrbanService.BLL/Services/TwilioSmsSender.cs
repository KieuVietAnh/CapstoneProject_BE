using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;

namespace UrbanService.BLL.Services;

/// <summary>
/// Gửi SMS qua Twilio Programmable Messaging REST API.
///
/// Dùng HttpClient trực tiếp thay vì SDK Twilio để giữ cùng cách làm với
/// <see cref="BrevoEmailSender"/> và không thêm phụ thuộc mới vào solution.
/// </summary>
public class TwilioSmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(
        HttpClient httpClient,
        IOptions<TwilioOptions> options,
        ILogger<TwilioSmsSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber))
        {
            throw new ArgumentException("Số điện thoại nhận là bắt buộc.", nameof(toPhoneNumber));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Nội dung tin nhắn là bắt buộc.", nameof(message));
        }

        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Twilio chưa được cấu hình đầy đủ.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"2010-04-01/Accounts/{_options.AccountSid}/Messages.json");

        /*
         * Twilio dùng HTTP Basic auth với AccountSid làm username và
         * AuthToken làm password.
         */
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.AccountSid}:{_options.AuthToken}")));

        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("To", toPhoneNumber),
            new KeyValuePair<string, string>("From", _options.FromNumber),
            new KeyValuePair<string, string>("Body", message)
        ]);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        /*
         * Đọc mã lỗi của Twilio để ghi log có ngữ cảnh, nhưng tuyệt đối không
         * log nội dung tin nhắn (chứa OTP) hay thông tin xác thực.
         */
        var errorCode = await ReadTwilioErrorCodeAsync(response, cancellationToken);

        _logger.LogError(
            "Twilio từ chối gửi SMS. HTTP {StatusCode}, Twilio code {TwilioCode}.",
            (int)response.StatusCode,
            errorCode?.ToString() ?? "unknown");

        throw new InvalidOperationException(
            BuildFailureMessage(errorCode));
    }

    private static async Task<int?> ReadTwilioErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("code", out var code) &&
                code.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Chuyển mã lỗi Twilio thành thông báo người dùng hiểu được, không lộ
    /// chi tiết hạ tầng.
    /// </summary>
    private static string BuildFailureMessage(int? twilioErrorCode)
    {
        return twilioErrorCode switch
        {
            // 21608: tài khoản trial chỉ gửi được tới số đã verify trong Twilio Console.
            21608 => "Số điện thoại chưa được xác minh trên tài khoản Twilio dùng thử. " +
                "Vui lòng dùng số đã đăng ký hoặc nâng cấp tài khoản Twilio.",

            // 21211: số nhận sai định dạng.
            21211 => "Số điện thoại không hợp lệ.",

            // 21610: người nhận đã chặn nhận tin từ số này.
            21610 => "Số điện thoại đã từ chối nhận tin nhắn từ hệ thống.",

            // 21614: số không nhận được SMS.
            21614 => "Số điện thoại không nhận được tin nhắn SMS.",

            _ => "Không gửi được mã OTP tới số điện thoại. Vui lòng thử lại sau."
        };
    }
}
