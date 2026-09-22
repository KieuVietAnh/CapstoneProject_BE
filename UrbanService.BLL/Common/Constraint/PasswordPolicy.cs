namespace UrbanService.BLL.Common.Constraint;

/// <summary>
/// Quy tắc mật khẩu dùng chung cho mọi đường đặt mật khẩu: đăng ký, đặt lại mật
/// khẩu qua OTP, admin tạo tài khoản và admin reset mật khẩu.
///
/// Để ở một chỗ vì trước đây mỗi service tự viết số 6, còn frontend chặn ở 8,
/// nên luật thực tế phụ thuộc vào việc gọi qua đường nào.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
}
