namespace UrbanService.BLL.Interfaces;

/// <summary>Số điện thoại đã được Firebase xác minh, lấy ra từ ID token.</summary>
public sealed record VerifiedPhoneNumber(string FirebaseUid, string PhoneNumber);

public interface IFirebasePhoneVerifier
{
    /// <summary>
    /// Xác minh Firebase ID token mà client nhận được sau khi nhập đúng OTP, rồi trả
    /// về số điện thoại Firebase đã xác thực.
    ///
    /// Không bao giờ tin số điện thoại do client tự gửi lên: chỉ số nằm trong token
    /// đã qua kiểm tra chữ ký mới chứng minh người dùng đang cầm chiếc SIM đó.
    /// </summary>
    Task<VerifiedPhoneNumber> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}
