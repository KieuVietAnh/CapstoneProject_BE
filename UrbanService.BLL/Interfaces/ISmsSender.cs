namespace UrbanService.BLL.Interfaces;

public interface ISmsSender
{
    /// <summary>
    /// Gửi một tin nhắn SMS tới số điện thoại dạng E.164.
    /// </summary>
    /// <param name="toPhoneNumber">Số nhận, đã chuẩn hóa về E.164 (ví dụ +84901234567).</param>
    Task SendAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default);
}
