using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.DAL.Entities
{
    public partial class Booking
    {
        public Guid BookingId { get; set; }

        public string BookingCode { get; set; } = null!;

        public Guid UserId { get; set; }

        public Guid? AssignedServiceStaffId { get; set; }

        public string ContactName { get; set; } = null!;

        public string ContactPhone { get; set; } = null!;

        public string ServiceAddress { get; set; } = null!;

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public DateTime ScheduleAt { get; set; }

        public string Status { get; set; } = null!;

        public decimal TotalAmount { get; set; }

        public string Currency { get; set; } = null!;

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public virtual User User { get; set; } = null!;


        // FK tới User nhưng đặt tên theo nghiệp vụ
        public virtual User? AssignedServiceStaff { get; set; }

        public virtual ICollection<BookingDetail> BookingDetails { get; set; }
            = new List<BookingDetail>();

        public virtual ICollection<ServicePayment> ServicePayments { get; set; }
            = new List<ServicePayment>();

        public virtual ICollection<BookingAssignmentHistory> BookingAssignmentHistories { get; set; }
    = new List<BookingAssignmentHistory>();
        public virtual ServiceExecution? ServiceExecution { get; set; }

        public virtual ServiceReview? ServiceReview { get; set; }
    }
}
