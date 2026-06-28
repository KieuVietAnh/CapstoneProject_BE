using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class UpdateBookingAssignmentRequest
    {

        public Guid NewServiceStaffId { get; set; }

        public string? Note { get; set; }
    }
}
