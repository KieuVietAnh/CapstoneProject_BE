using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class BookingDetailItemDto
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; } = null!;

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        public decimal LineTotal { get; set; }
    }
}
