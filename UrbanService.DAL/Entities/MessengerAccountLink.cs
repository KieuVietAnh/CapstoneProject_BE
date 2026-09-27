namespace UrbanService.DAL.Entities;

public class MessengerAccountLink
{
    public long LinkId { get; set; }

    public string PageId { get; set; } = null!;

    public string SenderPsid { get; set; } = null!;

    public Guid UserId { get; set; }

    public bool IsActive { get; set; }

    public DateTime LinkedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
