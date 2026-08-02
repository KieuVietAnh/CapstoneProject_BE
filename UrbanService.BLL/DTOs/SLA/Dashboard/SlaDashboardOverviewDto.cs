using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.SLA.Dashboard
{
    public class SlaDashboardOverviewDto
    {
        public int TotalSla { get; set; }

        public int RunningSla { get; set; }

        public int CompletedSla { get; set; }

        public int BreachedSla { get; set; }

        public int WarningSla { get; set; }

        public decimal SuccessRate { get; set; }

        public double AverageResolutionMinutes { get; set; }
    }
}
