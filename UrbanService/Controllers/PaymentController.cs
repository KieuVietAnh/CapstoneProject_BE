using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.DTOs.Payment;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("{bookingId:guid}")]
    public async Task<IActionResult> CreatePayment(
        Guid bookingId)
    {
        var userId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _paymentService.CreatePaymentAsync(
            bookingId,
            userId);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("success")]
    public async Task<IActionResult> PaymentSuccess(
        [FromQuery] long orderCode)
    {
        var result = await _paymentService.HandlePaymentSuccessAsync(orderCode);

        return Ok(result);
    }

    [HttpGet("{bookingId:guid}")]
    public async Task<IActionResult> GetPayment(
        Guid bookingId)
    {
        var result = await _paymentService.GetPaymentAsync(bookingId);

        return Ok(result);
    }
}