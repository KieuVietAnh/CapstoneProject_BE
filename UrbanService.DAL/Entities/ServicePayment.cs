using System;

namespace UrbanService.DAL.Entities;

public partial class ServicePayment
{
    public Guid PaymentId { get; set; }

    public Guid BookingId { get; set; }

    public Guid UserId { get; set; }

    public long OrderCode { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string PaymentMethod { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? TransactionReference { get; set; }

    public string? PaymentLinkId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
