using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs
{
    public class AssignFeedbackRequest
    {
        public Guid FeedbackId { get; set; }

        public int CoordinatorId { get; set; }

        public Guid StaffUserId { get; set; }

        public string? Note { get; set; }
    }
}
