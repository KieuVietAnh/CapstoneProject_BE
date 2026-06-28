using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class ExecuteBookingRequest
    {
        public Guid BookingId { get; set; }

        public string ExecutionSummary { get; set; } = null!;

        public string ActionTaken { get; set; } = null!;

        public string? ResultNote { get; set; }

        public List<ExecutionAttachmentRequest> Attachments { get; set; }
        = new();
    }
}
