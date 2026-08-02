using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.SLA.Dashboard
{
    public class RecentSlaBreachDto
    {
        public Guid FeedbackId { get; set; }

        public long FeedbackSlaId { get; set; }

        public string Title { get; set; } = null!;

        public string Type { get; set; } = null!;

        public DateTime BreachedAt { get; set; }

        public double OverdueMinutes { get; set; }
    }
}
