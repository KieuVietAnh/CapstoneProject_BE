using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.DAL.Entities
{
    public partial class ServiceExecution
    {
        public Guid ExecutionId { get; set; }

        public Guid BookingId { get; set; }

        public Guid ExecutedByServiceStaffId { get; set; }

        public string ExecutionSummary { get; set; } = null!;

        public string ActionTaken { get; set; } = null!;

        public string? ResultNote { get; set; }

        public string Status { get; set; } = null!;

        public DateTime ExecutedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public virtual Booking Booking { get; set; } = null!;

        // Thực chất là User nhưng đặt tên theo nghiệp vụ
        public virtual User ExecutedByServiceStaff { get; set; } = null!;

        public virtual ICollection<ServiceExecutionAttachment>
            ServiceExecutionAttachments
        { get; set; }
            = new List<ServiceExecutionAttachment>();
    }
}
