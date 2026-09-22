namespace UrbanService.Authorization;

/// <summary>
/// Đánh dấu endpoint mà tài khoản chưa xác thực email vẫn được gọi dù là thao tác
/// ghi. Chỉ dùng cho các API phục vụ hoàn tất đăng ký, vì nếu chặn luôn thì người
/// dùng không có đường nào để tự xác thực.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class AllowUnverifiedEmailAttribute : Attribute
{
}
