using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class BookingListItemDto
    {
        public Guid BookingId { get; set; }

        public string BookingCode { get; set; } = null!;

        public string ContactName { get; set; } = null!;

        public string ContactPhone { get; set; } = null!;

        public string ServiceAddress { get; set; } = null!;

        public DateTime ScheduleAt { get; set; }

        public string Status { get; set; } = null!;

        public decimal TotalAmount { get; set; }

        public string Currency { get; set; } = null!;

        public Guid UserId { get; set; }

        public string UserName { get; set; } = null!;

        public Guid? AssignedServiceStaffId { get; set; }

        public string? AssignedServiceStaffName { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Services { get; set; } = null!;
    }
}
