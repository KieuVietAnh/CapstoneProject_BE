using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class BookingAssignmentHistoryDto
    {
        public int HistoryId { get; set; }

        public Guid BookingId { get; set; }

        public Guid? OldServiceStaffId { get; set; }

        public string? OldServiceStaffName { get; set; }

        public Guid NewServiceStaffId { get; set; }

        public string NewServiceStaffName { get; set; } = null!;

        public Guid AssignedByUserId { get; set; }

        public string AssignedByName { get; set; } = null!;

        public DateTime AssignedAt { get; set; }

        public string? Note { get; set; }
    }
}
