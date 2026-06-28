using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.Common.Constraint
{
    public static class PaymentStatus
    {
        public const string Pending = "Pending";

        public const string Paid = "Paid";

        public const string Failed = "Failed";

        public const string Cancelled = "Cancelled";
    }
}
