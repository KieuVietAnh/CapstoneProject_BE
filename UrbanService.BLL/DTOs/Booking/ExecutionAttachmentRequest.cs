using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Booking
{
    public class ExecutionAttachmentRequest
    {
        public string FileUrl { get; set; } = null!;

        public string? FileType { get; set; }

        public string? Description { get; set; }
    }
}
