using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Payment
{
    public class ServicePaymentDto
    {
        public Guid PaymentId { get; set; }

        public Guid BookingId { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = null!;

        public string PaymentMethod { get; set; } = null!;

        public string Status { get; set; } = null!;

        public long OrderCode { get; set; }

        public string? TransactionReference { get; set; }

        public string? PaymentLinkId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }
    }
}
