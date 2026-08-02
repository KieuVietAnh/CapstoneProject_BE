using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.SLA.Dashboard
{
    public class SlaComplianceDto
    {
        public decimal TodayRate { get; set; }

        public decimal ThisWeekRate { get; set; }

        public decimal ThisMonthRate { get; set; }
    }
}
