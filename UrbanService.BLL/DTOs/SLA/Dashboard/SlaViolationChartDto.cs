using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.SLA.Dashboard
{
    public class SlaViolationChartDto
    {
        public DateTime Date { get; set; }

        public int Count { get; set; }
    }
}
