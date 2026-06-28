using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class CreateBookingRequest
    {
        public string ContactName { get; set; } = null!;

        public string ContactPhone { get; set; } = null!;

        public string ServiceAddress { get; set; } = null!;

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public DateTime ScheduleAt { get; set; }

        public string? Note { get; set; }

        public List<BookingServiceRequest> Services { get; set; } = [];
    }

}
