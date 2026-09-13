namespace UrbanService.BLL.Options;

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    /// <summary>
    /// Account SID của tài khoản Twilio, bắt đầu bằng "AC".
    /// </summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>
    /// Auth token của tài khoản Twilio. Đây là secret, chỉ nạp từ biến môi
    /// trường hoặc user-secrets, không đưa vào source.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Số điện thoại gửi đi, dạng E.164. Với tài khoản trial thì đây là
    /// Twilio trial number được cấp sẵn.
    /// </summary>
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>
    /// Mã quốc gia mặc định dùng khi người dùng nhập số nội địa bắt đầu bằng 0.
    /// </summary>
    public string DefaultCountryCode { get; set; } = "+84";

    public int TimeoutSeconds { get; set; } = 20;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) &&
        !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(FromNumber);
}
