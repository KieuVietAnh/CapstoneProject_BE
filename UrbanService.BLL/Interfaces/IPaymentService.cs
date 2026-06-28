using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UrbanService.BLL.DTOs.Payment;

namespace UrbanService.BLL.Interfaces
{
    public interface IPaymentService
    {
        Task<CreatePaymentResponse> CreatePaymentAsync(
            Guid bookingId,
            Guid userId);

        Task<PaymentCallbackResponse> HandlePaymentSuccessAsync(
            long orderCode);

        Task<ServicePaymentDto> GetPaymentAsync(
            Guid bookingId);
    } 
}
