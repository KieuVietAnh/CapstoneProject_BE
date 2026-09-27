namespace UrbanService.DAL.Entities;

public class MessengerLinkToken
{
    public Guid LinkTokenId { get; set; }

    public string PageId { get; set; } = null!;

    public string SenderPsid { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime? InvalidatedAt { get; set; }
}
