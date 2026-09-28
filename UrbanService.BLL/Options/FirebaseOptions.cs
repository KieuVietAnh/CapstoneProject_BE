namespace UrbanService.BLL.Options;

/// <summary>
/// Cấu hình Firebase Admin SDK, dùng để xác minh ID token mà client nhận được sau
/// khi người dùng nhập đúng OTP.
/// </summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>
    /// Nội dung JSON của service account, truyền qua biến môi trường. Ưu tiên hơn
    /// <see cref="ServiceAccountPath"/> vì môi trường deploy chạy trong container
    /// nên không có sẵn file trên đĩa, và khoá riêng thì không được commit.
    /// </summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>Đường dẫn file service account, dùng khi chạy máy lập trình viên.</summary>
    public string? ServiceAccountPath { get; set; }

    /// <summary>
    /// Chỉ chấp nhận ID token của lần nhập OTP trong vòng N phút gần nhất. Token cũ
    /// tuy chưa hết hạn nhưng không chứng minh được người dùng vừa cầm điện thoại.
    /// </summary>
    public int MaxAuthAgeMinutes { get; set; } = 10;
}

/// <summary>
/// Hạn mức gửi SMS OTP. Mỗi tin nhắn Firebase gửi đi là chi phí thật nên hạn mức
/// phải nằm ở backend; chặn ở frontend thì người gọi thẳng API vẫn đốt tiền.
/// </summary>
public sealed class PhoneOtpOptions
{
    public const string SectionName = "PhoneOtp";

    /// <summary>Số SMS thật tối đa toàn hệ thống mỗi ngày, tính theo giờ Việt Nam.</summary>
    public int DailyLimit { get; set; } = 5;

    /// <summary>
    /// Số test khai trong Firebase Console. Firebase không gửi SMS thật cho những số
    /// này nên chúng không tốn tiền và không bị tính vào hạn mức.
    /// </summary>
    public string[] TestNumbers { get; set; } = [];
}

/// <summary>Hạn mức gửi phản ánh, để một tài khoản không spam hệ thống tiếp nhận.</summary>
public sealed class FeedbackLimitOptions
{
    public const string SectionName = "FeedbackLimits";

    /// <summary>Số phản ánh tối đa một tài khoản gửi từ web mỗi ngày (giờ Việt Nam).</summary>
    public int DailyPerUser { get; set; } = 3;
}
