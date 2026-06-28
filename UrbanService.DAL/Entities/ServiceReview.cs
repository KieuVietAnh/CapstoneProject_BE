using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.DAL.Entities
{
    public partial class ServiceReview
    {
        public Guid ReviewId { get; set; }

        public Guid BookingId { get; set; }

        public Guid UserId { get; set; }

        public int Rating { get; set; }

        public bool? IsSatisfied { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual Booking Booking { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
