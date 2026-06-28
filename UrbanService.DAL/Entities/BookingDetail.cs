using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.DAL.Entities
{
    public partial class BookingDetail
    {
        public int BookingDetailId { get; set; }

        public Guid BookingId { get; set; }

        public int ServiceId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }

        public string? Note { get; set; }

        public virtual Booking Booking { get; set; } = null!;

        public virtual Service Service { get; set; } = null!;
    }
}
