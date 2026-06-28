using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UrbanService.BLL.DTOs.Booking;
using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces
{
    public interface IBookingService
    {
        #region Booking

        Task<BookingDetailDto> CreateBookingAsync(
            Guid userId,
            CreateBookingRequest request);

        Task<PagedResultDto<BookingListItemDto>> GetMyBookingsAsync(
    Guid userId,
    BookingQueryParameters query);

        Task<PagedResultDto<BookingListItemDto>> GetAllBookingsAsync(
            BookingFilterRequest request);

        Task<BookingDetailDto> GetBookingDetailAsync(
            Guid bookingId);

        #endregion

        #region Assignment

        Task AssignBookingAsync(
    Guid assignedByUserId,
    Guid bookingId,
    AssignBookingRequest request);

        Task UpdateBookingAssignmentAsync(
    Guid assignedByUserId,
    Guid bookingId,
    UpdateBookingAssignmentRequest request);

        #endregion

        #region Execution

        Task SubmitExecutionAsync(
            Guid serviceStaffId,
            ExecuteBookingRequest request);

        Task UpdateExecutionAsync(
    Guid serviceStaffId,
    Guid executionId,
    UpdateExecutionRequest request);

        Task<ExecutionResultDto> GetExecutionResultAsync(
            Guid bookingId);

        #endregion

        #region Review

        Task<ServiceReviewDto> ReviewBookingAsync(
    Guid userId,
    Guid bookingId,
    CreateServiceReviewRequest request);

        Task<ServiceReviewDto> GetBookingReviewAsync(
            Guid bookingId);

        #endregion
    }
}
