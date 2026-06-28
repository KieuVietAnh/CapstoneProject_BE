using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class BookingFilterRequest
    {
        public string? Keyword { get; set; }

        public string? Status { get; set; }

        public Guid? UserId { get; set; }

        public Guid? AssignedServiceStaffId { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int PageIndex { get; set; } = 1;

        public int PageSize { get; set; } = 10;
    }
}
