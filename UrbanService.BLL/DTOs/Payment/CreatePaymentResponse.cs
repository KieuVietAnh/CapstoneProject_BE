using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs.Payment
{
    public class CreatePaymentResponse
    {
        public string CheckoutUrl { get; set; } = null!;
    }
}
