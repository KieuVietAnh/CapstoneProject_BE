namespace UrbanService.BLL.Common.Constraint;

/// <summary>
/// Mã trạng thái nghiệp vụ trả kèm response xác thực, để client rẽ nhánh mà không
/// phải so khớp câu thông báo tiếng Việt.
/// </summary>
public static class AuthResultCode
{
    public const string EmailNotVerified = "EMAIL_NOT_VERIFIED";
}
