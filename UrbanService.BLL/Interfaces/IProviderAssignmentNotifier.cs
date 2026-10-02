namespace UrbanService.BLL.Interfaces;

/// <summary>
/// Báo cho đơn vị xử lý bên ngoài biết họ vừa được giao một sự vụ.
///
/// Trước đây việc phân công chỉ ghi vào nhật ký nội bộ, còn việc báo cho bên kia là
/// do nhân viên tự gọi điện. Đồng hồ SLA thì vẫn chạy ngay từ lúc đó, nên nếu nhân
/// viên quên gọi thì sự vụ trễ hạn mà không ai chứng minh được lỗi nằm ở đâu. Lớp
/// này đóng khoảng trống đó bằng một thông báo tự động, và ghi lại việc đã gửi.
/// </summary>
public interface IProviderAssignmentNotifier
{
    /// <summary>
    /// Gửi thông báo giao việc và ghi nhận kết quả vào nhật ký liên hệ.
    ///
    /// Không ném lỗi ra ngoài: phân công đã được lưu rồi, một lá email không gửi
    /// được không thể làm hỏng thao tác nghiệp vụ đã thành công.
    /// </summary>
    Task NotifyAssignmentAsync(
        int providerReportId,
        Guid assignedByUserId,
        CancellationToken cancellationToken = default);
}
