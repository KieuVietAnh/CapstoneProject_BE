namespace UrbanService.DAL.Entities;

/// <summary>
/// Một lần hệ thống cho phép client gọi Firebase gửi SMS OTP.
///
/// Firebase tính phí từng tin nhắn nên hạn mức phải đếm được và phải sống sót qua
/// restart container; giữ trong bộ nhớ thì mỗi lần deploy là hạn mức lại về 0.
/// </summary>
public partial class PhoneOtpRequest
{
    public long PhoneOtpRequestId { get; set; }

    public string PhoneNumber { get; set; } = null!;

    /// <summary>Ngày theo giờ Việt Nam, dạng yyyy-MM-dd, để đếm theo ngày làm việc.</summary>
    public string Day { get; set; } = null!;

    public Guid? RequestedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
}
