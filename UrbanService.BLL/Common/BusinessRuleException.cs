namespace UrbanService.BLL.Common;

/// <summary>
/// Lỗi nghiệp vụ mang theo mã máy đọc được và HTTP status riêng.
///
/// Dùng khi client cần rẽ nhánh giao diện theo từng loại lỗi — ví dụ chưa xác thực
/// số điện thoại thì điều hướng sang trang xác thực, còn hết lượt gửi trong ngày
/// thì mở trang thông báo riêng. So khớp câu tiếng Việt để rẽ nhánh là cách làm
/// vỡ ngay khi ai đó sửa lại câu chữ.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string code, string message, int statusCode)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    /// <summary>Mã nghiệp vụ, xem <see cref="Constraint.BusinessErrorCode"/>.</summary>
    public string Code { get; }

    public int StatusCode { get; }
}
