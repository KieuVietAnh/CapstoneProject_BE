using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.Common.Constraint
{
    public static class BookingStatus
    {
        public const string Pending = "Pending";

        public const string Assigned = "Assigned";

        public const string InProgress = "InProgress";

        public const string Completed = "Completed";

        public const string Cancelled = "Cancelled";

        public static string Paid = "Paid";
    }
}
