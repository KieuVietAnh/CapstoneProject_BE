using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs
{
    public class CitizenReviewRequest
    {
        public Guid FeedbackId { get; set; }

        public Guid UserId { get; set; }

        public int Rating { get; set; }

        public bool IsSatisfied { get; set; }

        public string? Comment { get; set; }
    }
}
