using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class ServiceReviewDto
    {
        public Guid ReviewId { get; set; }

        public Guid BookingId { get; set; }

        public Guid UserId { get; set; }

        public string UserName { get; set; } = null!;

        public int Rating { get; set; }

        public bool? IsSatisfied { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
