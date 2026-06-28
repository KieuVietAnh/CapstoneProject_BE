using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.DAL.Entities
{
    public partial class ServiceExecutionAttachment
    {
        public Guid ServiceExecutionAttachmentId { get; set; }

        public Guid ExecutionId { get; set; }

        public string FileUrl { get; set; } = null!;

        public string? FileType { get; set; }

        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; }

        public virtual ServiceExecution ServiceExecution { get; set; } = null!;
    }
}
