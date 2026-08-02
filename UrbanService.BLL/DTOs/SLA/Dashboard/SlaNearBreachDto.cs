using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.SLA.Dashboard
{
    public class SlaNearBreachDto
    {
        public Guid FeedbackId { get; set; }

        public long FeedbackSlaId { get; set; }

        public string Title { get; set; } = null!;

        public string? Priority { get; set; }

        public DateTime Deadline { get; set; }

        public double RemainingMinutes { get; set; }
    }
}
