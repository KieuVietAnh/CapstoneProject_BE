namespace UrbanService.BLL.Common.Constraint;

/// <summary>Mã lỗi nghiệp vụ trả về cho client để rẽ nhánh giao diện.</summary>
public static class BusinessErrorCode
{
    /// <summary>Tài khoản chưa xác thực số điện thoại.</summary>
    public const string PhoneNotVerified = "PHONE_NOT_VERIFIED";

    /// <summary>Tài khoản đã gửi hết số phản ánh cho phép trong ngày.</summary>
    public const string DailyFeedbackLimitReached = "DAILY_FEEDBACK_LIMIT_REACHED";

    /// <summary>Toàn hệ thống đã dùng hết hạn mức SMS OTP trong ngày.</summary>
    public const string DailyOtpLimitReached = "DAILY_OTP_LIMIT_REACHED";

    /// <summary>Số điện thoại đã thuộc về một tài khoản đã xác thực khác.</summary>
    public const string PhoneAlreadyUsed = "PHONE_ALREADY_USED";
}
