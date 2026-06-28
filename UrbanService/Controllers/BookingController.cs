using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UrbanService.BLL.DTOs.Booking;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/bookings")]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        #region Booking

        /// <summary>
        /// USER
        ///
        /// Tạo Booking mới.
        ///
        /// Frontend chỉ truyền thông tin booking.
        ///
        /// UserId lấy từ JWT.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateBooking(
            CreateBookingRequest request)
        {
            var userId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _bookingService.CreateBookingAsync(
                userId,
                request);

            return Ok(result);
        }

        /// <summary>
        /// USER
        ///
        /// Danh sách Booking của chính User.
        ///
        /// UserId lấy từ JWT.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] BookingQueryParameters query)
        {
            var userId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _bookingService.GetMyBookingsAsync(
                userId,
                query);

            return Ok(result);
        }

        /// <summary>
        /// ADMIN / STAFF
        ///
        /// Danh sách tất cả Booking.
        ///
        /// Có Filter + Paging.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllBookings(
            [FromQuery] BookingFilterRequest request)
        {
            var result = await _bookingService.GetAllBookingsAsync(request);

            return Ok(result);
        }

        /// <summary>
        /// Chi tiết Booking.
        ///
        /// Bao gồm:
        ///
        /// - Services
        /// - Assignment History
        /// - Execution
        /// - Review
        /// </summary>
        [HttpGet("{bookingId:guid}")]
        public async Task<IActionResult> GetBookingDetail(
            Guid bookingId)
        {
            var result =
                await _bookingService.GetBookingDetailAsync(bookingId);

            return Ok(result);
        }

        #endregion

        #region Assignment

        /// <summary>
        /// Manager gán Staff cho Booking.
        ///
        /// bookingId lấy trên Route.
        ///
        /// assignedByUserId lấy từ JWT.
        /// </summary>
        [HttpPut("{bookingId:guid}/assign")]
        public async Task<IActionResult> AssignBooking(
            Guid bookingId,
            AssignBookingRequest request)
        {
            var assignedByUserId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _bookingService.AssignBookingAsync(
                assignedByUserId,
                bookingId,
                request);

            return Ok();
        }

        /// <summary>
        /// Manager đổi Staff thực hiện Booking.
        ///
        /// bookingId lấy trên Route.
        ///
        /// assignedByUserId lấy từ JWT.
        /// </summary>
        [HttpPut("{bookingId:guid}/reassign")]
        public async Task<IActionResult> ReassignBooking(
            Guid bookingId,
            UpdateBookingAssignmentRequest request)
        {
            var assignedByUserId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _bookingService.UpdateBookingAssignmentAsync(
                assignedByUserId,
                bookingId,
                request);

            return Ok();
        }

        #endregion

        #region Execution

        /// <summary>
        /// SERVICE STAFF
        ///
        /// Submit kết quả thực hiện.
        ///
        /// File đã upload Cloudinary trước,
        /// chỉ truyền URL.
        ///
        /// serviceStaffId lấy từ JWT.
        /// </summary>
        [HttpPost("{bookingId:guid}/execution")]
        public async Task<IActionResult> SubmitExecution(
            Guid bookingId,
            ExecuteBookingRequest request)
        {
            request.BookingId = bookingId;

            var serviceStaffId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _bookingService.SubmitExecutionAsync(
                serviceStaffId,
                request);

            return Ok();
        }

        /// <summary>
        /// SERVICE STAFF
        ///
        /// Cập nhật kết quả thực hiện.
        ///
        /// executionId lấy trên Route.
        ///
        /// serviceStaffId lấy từ JWT.
        /// </summary>
        [HttpPut("executions/{executionId:guid}")]
        public async Task<IActionResult> UpdateExecution(
            Guid executionId,
            UpdateExecutionRequest request)
        {
            var serviceStaffId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _bookingService.UpdateExecutionAsync(
                serviceStaffId,
                executionId,
                request);

            return Ok();
        }

        /// <summary>
        /// Lấy kết quả thực hiện của Booking.
        ///
        /// Bao gồm:
        ///
        /// - Summary
        /// - Action
        /// - Result
        /// - Attachments
        /// </summary>
        [HttpGet("{bookingId:guid}/execution")]
        public async Task<IActionResult> GetExecutionResult(
            Guid bookingId)
        {
            var result =
                await _bookingService.GetExecutionResultAsync(bookingId);

            return Ok(result);
        }

        #endregion

        #region Review

        /// <summary>
        /// USER
        ///
        /// Đánh giá Booking.
        ///
        /// Chỉ Booking Completed.
        ///
        /// bookingId lấy Route.
        ///
        /// userId lấy JWT.
        /// </summary>
        [HttpPost("{bookingId:guid}/review")]
        public async Task<IActionResult> ReviewBooking(
            Guid bookingId,
            CreateServiceReviewRequest request)
        {
            var userId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result =
                await _bookingService.ReviewBookingAsync(
                    userId,
                    bookingId,
                    request);

            return Ok(result);
        }

        /// <summary>
        /// Lấy Review của Booking.
        /// </summary>
        [HttpGet("{bookingId:guid}/review")]
        public async Task<IActionResult> GetBookingReview(
            Guid bookingId)
        {
            var result =
                await _bookingService.GetBookingReviewAsync(
                    bookingId);

            return Ok(result);
        }

        #endregion
    }
}
