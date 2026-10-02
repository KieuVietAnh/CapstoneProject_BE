namespace UrbanService.BLL.Options;

/// <summary>
/// Cấu hình thông báo gửi cho đơn vị xử lý bên ngoài.
///
/// Email giao việc đi tới hộp thư của một tổ chức thật, nên phải có cờ tắt được.
/// Khi chạy demo hoặc kiểm thử trên dữ liệu thật, bật lên là mỗi lần bấm phân công
/// lại bắn một lá thư vào hộp thư nhà cung cấp.
/// </summary>
public sealed class ProviderNotificationOptions
{
    public const string SectionName = "ProviderNotification";

    /// <summary>
    /// Gửi email giao việc khi Staff phân công đơn vị xử lý.
    ///
    /// Mặc định bật, để đồng bộ với email cảnh báo SLA vốn đã gửi cho chính các đơn
    /// vị này. Đặt <c>false</c> khi muốn chạy thử mà không chạm hộp thư thật.
    /// </summary>
    public bool AssignmentEmailEnabled { get; set; } = true;
}
