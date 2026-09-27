namespace UrbanService.BLL.DTOs;

public class ConfirmMessengerAccountLinkRequest
{
    public string Token { get; set; } = string.Empty;
}

public class MessengerAccountLinkDto
{
    public long LinkId { get; set; }
    public string PageId { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime LinkedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
