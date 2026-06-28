using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Payment
{
    public class PaymentCallbackResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = null!;
    }
}
