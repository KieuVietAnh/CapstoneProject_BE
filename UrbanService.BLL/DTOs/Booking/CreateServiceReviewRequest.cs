using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class CreateServiceReviewRequest
    {
        public Guid BookingId { get; set; }

        public int Rating { get; set; }

        public bool? IsSatisfied { get; set; }

        public string? Comment { get; set; }
    }
}
