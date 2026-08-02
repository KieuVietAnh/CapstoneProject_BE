using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.SLA.Dashboard
{
    public class SlaPerformanceDto
    {
        public double AverageResponseMinutes { get; set; }

        public double AverageResolutionMinutes { get; set; }

        public decimal ResponseSuccessRate { get; set; }

        public decimal ResolutionSuccessRate { get; set; }
    }
}
