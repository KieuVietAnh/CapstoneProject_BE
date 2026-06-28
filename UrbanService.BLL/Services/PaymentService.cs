using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs.Payment;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using Microsoft.Extensions.Options;
using UrbanService.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using static Net.payOS.PayOS;
using Net.payOS;
using Net.payOS.Types;

namespace UrbanService.BLL.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;

        private readonly PayOS _payOS;

        private readonly PayOSConfig _payOSConfig;

        public PaymentService(
            IUnitOfWork unitOfWork,
            IOptions<PayOSConfig> payOSConfig)
        {
            _unitOfWork = unitOfWork;

            _payOSConfig = payOSConfig.Value;

            _payOS = new PayOS(
                _payOSConfig.ClientId,
                _payOSConfig.ApiKey,
                _payOSConfig.ChecksumKey);
        }

        public async Task<CreatePaymentResponse> CreatePaymentAsync(
    Guid bookingId,
    Guid userId)
        {
            var booking = await _unitOfWork
                .GetRepository<Booking>()
                .Query(q => q
                    .Include(x => x.BookingDetails)
                        .ThenInclude(x => x.Service)
                    .Include(x => x.ServicePayments))
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (booking == null)
                throw new Exception("Booking not found.");

            if (booking.UserId != userId)
                throw new Exception("You are not allowed to pay this booking.");

            if (booking.Status != BookingStatus.Pending)
                throw new Exception("This booking cannot be paid.");

            var payment = booking.ServicePayments.FirstOrDefault();

            if (payment == null)
                throw new Exception("Payment information not found.");

            if (payment.Status == PaymentStatus.Paid)
                throw new Exception("This booking has already been paid.");

            // OrderCode theo yêu cầu của PayOS
            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var items = booking.BookingDetails
                .Select(x => new ItemData(
                    x.Service.ServiceName,
                    1,
                    Convert.ToInt32(x.UnitPrice)))
                .ToList();

            var paymentData = new PaymentData(
                orderCode,
                Convert.ToInt32(payment.Amount),
                $"Booking {booking.BookingCode}",
                items,
                _payOSConfig.CancelUrl,
                _payOSConfig.ReturnUrl);

            var result = await _payOS.createPaymentLink(paymentData);

            payment.OrderCode = orderCode;
            payment.PaymentLinkId = result.paymentLinkId;
            payment.PaymentMethod = PaymentMethod.PayOS;
            payment.Status = PaymentStatus.Pending;
            payment.UpdatedAt = DateTime.UtcNow;

            _unitOfWork
                .GetRepository<ServicePayment>()
                .Update(payment);

            await _unitOfWork.SaveAsync();

            return new CreatePaymentResponse
            {
                CheckoutUrl = result.checkoutUrl
            };
        }

        public async Task<PaymentCallbackResponse> HandlePaymentSuccessAsync(
    long orderCode)
        {
            var payment = await _unitOfWork
                .GetRepository<ServicePayment>()
                .Query(q => q.Include(x => x.Booking))
                .FirstOrDefaultAsync(x => x.OrderCode == orderCode);

            if (payment == null)
                throw new Exception("Payment not found.");

            if (payment.Status == PaymentStatus.Paid)
            {
                return new PaymentCallbackResponse
                {
                    Success = true,
                    Message = "Payment has already been completed."
                };
            }

            var paymentInfo = await _payOS.getPaymentLinkInformation(orderCode);

            if (paymentInfo == null)
                throw new Exception("Cannot retrieve payment information.");

            if (!string.Equals(paymentInfo.status, "PAID", StringComparison.OrdinalIgnoreCase))
            {
                return new PaymentCallbackResponse
                {
                    Success = false,
                    Message = "Payment has not been completed."
                };
            }

            payment.Status = PaymentStatus.Paid;
            payment.TransactionReference = paymentInfo.id;
            payment.PaidAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;

            payment.Booking.Status = BookingStatus.Paid;
            payment.Booking.UpdatedAt = DateTime.UtcNow;

            _unitOfWork
                .GetRepository<ServicePayment>()
                .Update(payment);

            _unitOfWork
                .GetRepository<Booking>()
                .Update(payment.Booking);

            await _unitOfWork.SaveAsync();

            return new PaymentCallbackResponse
            {
                Success = true,
                Message = "Payment successful."
            };
        }

        public async Task<ServicePaymentDto> GetPaymentAsync(Guid bookingId)
{
    var payment = await _unitOfWork
        .GetRepository<ServicePayment>()
        .Query(q => q.Include(x => x.Booking))
        .FirstOrDefaultAsync(x => x.BookingId == bookingId);

    if (payment == null)
        throw new Exception("Payment not found.");

    return new ServicePaymentDto
    {
        PaymentId = payment.PaymentId,

        BookingId = payment.BookingId,

        Amount = payment.Amount,

        Currency = payment.Currency,

        PaymentMethod = payment.PaymentMethod,

        Status = payment.Status,

        OrderCode = payment.OrderCode,

        TransactionReference = payment.TransactionReference,

        PaymentLinkId = payment.PaymentLinkId,

        CreatedAt = payment.CreatedAt,

        PaidAt = payment.PaidAt
    };
}


    }
}
