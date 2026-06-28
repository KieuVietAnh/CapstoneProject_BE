using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class ExecutionResultDto
    {
        public Guid ExecutionId { get; set; }

        public Guid BookingId { get; set; }

        public string ExecutionSummary { get; set; } = null!;

        public string ActionTaken { get; set; } = null!;

        public string? ResultNote { get; set; }

        public string Status { get; set; } = null!;

        public string ExecutedBy { get; set; } = null!;

        public DateTime ExecutedAt { get; set; }

        public List<ExecutionAttachmentDto> Attachments { get; set; }
            = new();
    }
}
