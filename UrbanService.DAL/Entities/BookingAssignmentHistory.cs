using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.DAL.Entities
{
    public partial class BookingAssignmentHistory
    {
        public int HistoryId { get; set; }

        public Guid BookingId { get; set; }

        public Guid? OldServiceStaffId { get; set; }

        public Guid NewServiceStaffId { get; set; }

        public Guid AssignedByUserId { get; set; }

        public DateTime AssignedAt { get; set; }

        public string? Note { get; set; }

        public virtual Booking Booking { get; set; } = null!;

        public virtual User? OldServiceStaff { get; set; }

        public virtual User NewServiceStaff { get; set; } = null!;

        public virtual User AssignedByUser { get; set; } = null!;
    }
}
