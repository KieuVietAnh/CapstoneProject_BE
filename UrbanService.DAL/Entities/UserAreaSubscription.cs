using System;

namespace UrbanService.DAL.Entities;

public partial class UserAreaSubscription
{
    public int SubscriptionId { get; set; }

    public Guid UserId { get; set; }

    public int AreaId { get; set; }

    public bool IsPrimaryArea { get; set; }

    public bool ReceiveAlerts { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
