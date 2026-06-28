using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs.Booking;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services
{
    public class BookingService : IBookingService
    {
        private readonly IUnitOfWork _unitOfWork;

        public BookingService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region Booking

        public async Task<BookingDetailDto> CreateBookingAsync(
            Guid userId,
            CreateBookingRequest request)
        {
            if (request.Services == null || !request.Services.Any())
                throw new Exception("Please select at least one service.");

            if (request.ScheduleAt <= DateTime.UtcNow)
                throw new Exception("Schedule time must be greater than current time.");

            var serviceIds = request.Services
                .Select(x => x.ServiceId)
                .Distinct()
                .ToList();

            var services = await _unitOfWork
                .GetRepository<Service>()
                .Entities
                .Where(x =>
                    serviceIds.Contains(x.ServiceId)
                    && x.IsActive)
                .ToListAsync();

            if (services.Count != serviceIds.Count)
                throw new Exception("One or more selected services are invalid.");

            var booking = new Booking
            {
                BookingId = Guid.NewGuid(),

                BookingCode = GenerateBookingCode(),

                UserId = userId,

                ContactName = request.ContactName,

                ContactPhone = request.ContactPhone,

                ServiceAddress = request.ServiceAddress,

                Latitude = request.Latitude,

                Longitude = request.Longitude,

                ScheduleAt = request.ScheduleAt,

                Status = BookingStatus.Pending,

                Currency = "VND",

                Note = request.Note,

                CreatedAt = DateTime.UtcNow
            };

            decimal totalAmount = 0;

            foreach (var service in services)
            {
                var price = service.BasePrice ?? 0;

                booking.BookingDetails.Add(
                    new BookingDetail
                    {
                        ServiceId = service.ServiceId,

                        Quantity = 1,

                        UnitPrice = price,

                        LineTotal = price
                    });

                totalAmount += price;
            }

            booking.TotalAmount = totalAmount;

            await _unitOfWork
                .GetRepository<Booking>()
                .AddAsync(booking);

            await _unitOfWork.SaveAsync();

            return await GetBookingDetailAsync(
                booking.BookingId);
        }

        #endregion
        private static string GenerateBookingCode()
        {
            return $"BK{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        }

        public async Task<PagedResultDto<BookingListItemDto>> GetMyBookingsAsync(
    Guid userId,
    BookingQueryParameters request)
        {
            var query = _unitOfWork
                .GetRepository<Booking>()
                .Entities
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.AssignedServiceStaff)
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.Service)
                .Where(x => x.UserId == userId);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var keyword = request.Keyword.Trim();

                query = query.Where(x =>
                    x.BookingCode.Contains(keyword) ||
                    x.ContactName.Contains(keyword) ||
                    x.ContactPhone.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x =>
                    x.Status == request.Status);
            }

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new BookingListItemDto
                {
                    BookingId = x.BookingId,

                    BookingCode = x.BookingCode,

                    ContactName = x.ContactName,

                    ContactPhone = x.ContactPhone,

                    ServiceAddress = x.ServiceAddress,

                    ScheduleAt = x.ScheduleAt,

                    Status = x.Status,

                    TotalAmount = x.TotalAmount,

                    Currency = x.Currency,

                    UserId = x.UserId,

                    UserName = x.User.FullName,

                    AssignedServiceStaffId = x.AssignedServiceStaffId,

                    AssignedServiceStaffName =
                        x.AssignedServiceStaff == null
                            ? null
                            : x.AssignedServiceStaff.FullName,

                    Services = string.Join(", ",
                        x.BookingDetails
                            .Select(d => d.Service.ServiceName)),

                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return new PagedResultDto<BookingListItemDto>
            {
                Items = items,

                PageNumber = request.PageIndex,

                PageSize = request.PageSize,

                TotalItems = totalItems,

                TotalPages = (int)Math.Ceiling(
                    totalItems / (double)request.PageSize)
            };
        }

        public async Task<PagedResultDto<BookingListItemDto>> GetAllBookingsAsync(
    BookingFilterRequest request)
        {
            var query = _unitOfWork
                .GetRepository<Booking>()
                .Entities
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.AssignedServiceStaff)
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.Service)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var keyword = request.Keyword.Trim();

                query = query.Where(x =>
                    x.BookingCode.Contains(keyword) ||
                    x.ContactName.Contains(keyword) ||
                    x.ContactPhone.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x =>
                    x.Status == request.Status);
            }

            if (request.UserId.HasValue)
            {
                query = query.Where(x =>
                    x.UserId == request.UserId.Value);
            }

            if (request.AssignedServiceStaffId.HasValue)
            {
                query = query.Where(x =>
                    x.AssignedServiceStaffId ==
                    request.AssignedServiceStaffId.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(x =>
                    x.ScheduleAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(x =>
                    x.ScheduleAt <= request.ToDate.Value);
            }

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(x => new BookingListItemDto
                {
                    BookingId = x.BookingId,

                    BookingCode = x.BookingCode,

                    ContactName = x.ContactName,

                    ContactPhone = x.ContactPhone,

                    ServiceAddress = x.ServiceAddress,

                    ScheduleAt = x.ScheduleAt,

                    Status = x.Status,

                    TotalAmount = x.TotalAmount,

                    Currency = x.Currency,

                    UserId = x.UserId,

                    UserName = x.User.FullName,

                    AssignedServiceStaffId = x.AssignedServiceStaffId,

                    AssignedServiceStaffName =
                        x.AssignedServiceStaff == null
                            ? null
                            : x.AssignedServiceStaff.FullName,

                    Services = string.Join(", ",
                        x.BookingDetails
                            .Select(d => d.Service.ServiceName)),

                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return new PagedResultDto<BookingListItemDto>
            {
                Items = items,

                PageNumber = request.PageIndex,

                PageSize = request.PageSize,

                TotalItems = totalItems,

                TotalPages = (int)Math.Ceiling(
                    totalItems / (double)request.PageSize)
            };
        }

        public async Task<BookingDetailDto> GetBookingDetailAsync(Guid bookingId)
        {
            var booking = await _unitOfWork
                .GetRepository<Booking>()
                .Entities
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.AssignedServiceStaff)
                .Include(x => x.BookingDetails)
                    .ThenInclude(x => x.Service)
                .Include(x => x.BookingAssignmentHistories)
                    .ThenInclude(x => x.OldServiceStaff)
                .Include(x => x.BookingAssignmentHistories)
                    .ThenInclude(x => x.NewServiceStaff)
                .Include(x => x.BookingAssignmentHistories)
                    .ThenInclude(x => x.AssignedByUser)
                .Include(x => x.ServiceExecution)
                    .ThenInclude(x => x.ServiceExecutionAttachments)
                .Include(x => x.ServiceReview)
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (booking == null)
                throw new Exception("Booking not found.");

            return new BookingDetailDto
            {
                BookingId = booking.BookingId,

                BookingCode = booking.BookingCode,

                ContactName = booking.ContactName,

                ContactPhone = booking.ContactPhone,

                ServiceAddress = booking.ServiceAddress,

                Latitude = booking.Latitude,

                Longitude = booking.Longitude,

                ScheduleAt = booking.ScheduleAt,

                Status = booking.Status,

                TotalAmount = booking.TotalAmount,

                Currency = booking.Currency,

                Note = booking.Note,

                UserId = booking.UserId,

                UserName = booking.User.FullName,

                AssignedServiceStaffId = booking.AssignedServiceStaffId,

                AssignedServiceStaffName = booking.AssignedServiceStaff?.FullName,

                Services = booking.BookingDetails
                    .OrderBy(x => x.BookingDetailId)
                    .Select(x => new BookingDetailItemDto
                    {
                        ServiceId = x.ServiceId,

                        ServiceName = x.Service.ServiceName,

                        Quantity = x.Quantity,

                        UnitPrice = x.UnitPrice,

                        LineTotal = x.LineTotal
                    })
                    .ToList(),

                AssignmentHistories = booking.BookingAssignmentHistories
                    .OrderByDescending(x => x.AssignedAt)
                    .Select(x => new BookingAssignmentHistoryDto
                    {
                        HistoryId = x.HistoryId,

                        BookingId = x.BookingId,

                        OldServiceStaffId = x.OldServiceStaffId,

                        OldServiceStaffName = x.OldServiceStaff == null
                            ? null
                            : x.OldServiceStaff.FullName,

                        NewServiceStaffId = x.NewServiceStaffId,

                        NewServiceStaffName = x.NewServiceStaff.FullName,

                        AssignedByUserId = x.AssignedByUserId,

                        AssignedByName = x.AssignedByUser.FullName,

                        AssignedAt = x.AssignedAt,

                        Note = x.Note
                    })
                    .ToList(),

                Execution = booking.ServiceExecution == null
                    ? null
                    : new ExecutionResultDto
                    {
                        ExecutionId = booking.ServiceExecution.ExecutionId,

                        BookingId = booking.BookingId,

                        ExecutionSummary = booking.ServiceExecution.ExecutionSummary,

                        ActionTaken = booking.ServiceExecution.ActionTaken,

                        ResultNote = booking.ServiceExecution.ResultNote,

                        Status = booking.ServiceExecution.Status,

                        ExecutedBy = booking.AssignedServiceStaff?.FullName ?? string.Empty,

                        ExecutedAt = booking.ServiceExecution.ExecutedAt,

                        Attachments = booking.ServiceExecution.ServiceExecutionAttachments
                            .Select(a => new ExecutionAttachmentDto
                            {
                                FileUrl = a.FileUrl,

                                FileType = a.FileType,

                                Description = a.Description
                            })
                            .ToList()
                    },

                Review = booking.ServiceReview == null
                    ? null
                    : new ServiceReviewDto
                    {
                        ReviewId = booking.ServiceReview.ReviewId,

                        BookingId = booking.BookingId,

                        UserId = booking.ServiceReview.UserId,

                        UserName = booking.User.FullName,

                        Rating = booking.ServiceReview.Rating,

                        IsSatisfied = booking.ServiceReview.IsSatisfied,

                        Comment = booking.ServiceReview.Comment,

                        CreatedAt = booking.ServiceReview.CreatedAt
                    }
            };
        }

        public async Task AssignBookingAsync(
    Guid assignedByUserId,
    Guid bookingId,
    AssignBookingRequest request)
        {
            _unitOfWork.BeginTransaction();

            try
            {
                var booking = await _unitOfWork
                    .GetRepository<Booking>()
                    .Query()
                    .FirstOrDefaultAsync(x => x.BookingId == bookingId);

                if (booking == null)
                    throw new Exception("Booking not found.");

                var staff = await _unitOfWork
                    .GetRepository<User>()
                    .Query(q => q.Include(x => x.Role))
                    .FirstOrDefaultAsync(x => x.UserId == request.ServiceStaffId);

                if (staff == null)
                    throw new Exception("Service staff not found.");

                if (!staff.IsActive)
                    throw new Exception("Service staff is inactive.");


                booking.AssignedServiceStaffId = request.ServiceStaffId;

                if (booking.Status == BookingStatus.Pending)
                {
                    booking.Status = BookingStatus.Assigned;
                }

                booking.UpdatedAt = DateTime.UtcNow;

                _unitOfWork
                    .GetRepository<Booking>()
                    .Update(booking);

                await _unitOfWork
                    .GetRepository<BookingAssignmentHistory>()
                    .AddAsync(new BookingAssignmentHistory
                    {
                        BookingId = booking.BookingId,
                        OldServiceStaffId = null,
                        NewServiceStaffId = request.ServiceStaffId,
                        AssignedByUserId = assignedByUserId,
                        AssignedAt = DateTime.UtcNow,
                        Note = request.Note
                    });

                await _unitOfWork.SaveAsync();

                _unitOfWork.CommitTransaction();
            }
            catch
            {
                _unitOfWork.RollBack();
                throw;
            }
        }

        public async Task UpdateBookingAssignmentAsync(
    Guid assignedByUserId,
    Guid bookingId,
    UpdateBookingAssignmentRequest request)
        {
            _unitOfWork.BeginTransaction();

            try
            {
                var booking = await _unitOfWork
                    .GetRepository<Booking>()
                    .Query()
                    .FirstOrDefaultAsync(x => x.BookingId == bookingId);

                if (booking == null)
                    throw new Exception("Booking not found.");

                if (booking.AssignedServiceStaffId == null)
                    throw new Exception("Booking has not been assigned.");

                if (booking.AssignedServiceStaffId == request.NewServiceStaffId)
                    throw new Exception("Booking is already assigned to this staff.");

                var newStaff = await _unitOfWork
                    .GetRepository<User>()
                    .Query(q => q.Include(x => x.Role))
                    .FirstOrDefaultAsync(x => x.UserId == request.NewServiceStaffId);

                if (newStaff == null)
                    throw new Exception("Service staff not found.");

                if (!newStaff.IsActive)
                    throw new Exception("Service staff is inactive.");

                var oldStaffId = booking.AssignedServiceStaffId;

                booking.AssignedServiceStaffId = request.NewServiceStaffId;
                booking.UpdatedAt = DateTime.UtcNow;

                _unitOfWork
                    .GetRepository<Booking>()
                    .Update(booking);

                await _unitOfWork
                    .GetRepository<BookingAssignmentHistory>()
                    .AddAsync(new BookingAssignmentHistory
                    {
                        BookingId = booking.BookingId,
                        OldServiceStaffId = oldStaffId,
                        NewServiceStaffId = request.NewServiceStaffId,
                        AssignedByUserId = assignedByUserId,
                        AssignedAt = DateTime.UtcNow,
                        Note = request.Note
                    });

                await _unitOfWork.SaveAsync();

                _unitOfWork.CommitTransaction();
            }
            catch
            {
                _unitOfWork.RollBack();
                throw;
            }
        }



        public async Task SubmitExecutionAsync(
    Guid serviceStaffId,
    ExecuteBookingRequest request)
        {
            _unitOfWork.BeginTransaction();

            try
            {
                var booking = await _unitOfWork
                    .GetRepository<Booking>()
                    .Query(q => q
                        .Include(x => x.ServiceExecution))
                    .FirstOrDefaultAsync(x => x.BookingId == request.BookingId);

                if (booking == null)
                    throw new Exception("Booking not found.");

                if (booking.AssignedServiceStaffId == null)
                    throw new Exception("Booking has not been assigned.");

                if (booking.AssignedServiceStaffId != serviceStaffId)
                    throw new Exception("You are not assigned to this booking.");

                if (booking.ServiceExecution != null)
                    throw new Exception("Execution has already been submitted.");

                var execution = new ServiceExecution
                {
                    ExecutionId = Guid.NewGuid(),

                    BookingId = booking.BookingId,

                    ExecutedByServiceStaffId = serviceStaffId,

                    ExecutionSummary = request.ExecutionSummary,

                    ActionTaken = request.ActionTaken,

                    ResultNote = request.ResultNote,

                    Status = "Completed",

                    ExecutedAt = DateTime.UtcNow,

                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork
                    .GetRepository<ServiceExecution>()
                    .AddAsync(execution);

                if (request.Attachments != null && request.Attachments.Any())
                {
                    foreach (var item in request.Attachments)
                    {
                        await _unitOfWork
                            .GetRepository<ServiceExecutionAttachment>()
                            .AddAsync(new ServiceExecutionAttachment
                            {
                                ServiceExecutionAttachmentId = Guid.NewGuid(),

                                ExecutionId = execution.ExecutionId,

                                FileUrl = item.FileUrl,

                                FileType = item.FileType,

                                Description = item.Description,

                                UploadedAt = DateTime.UtcNow
                            });
                    }
                }

                booking.Status = BookingStatus.Completed;
                booking.UpdatedAt = DateTime.UtcNow;

                _unitOfWork
                    .GetRepository<Booking>()
                    .Update(booking);

                await _unitOfWork.SaveAsync();

                _unitOfWork.CommitTransaction();
            }
            catch
            {
                _unitOfWork.RollBack();
                throw;
            }
        }

        public async Task UpdateExecutionAsync(
    Guid serviceStaffId,
    Guid executionId,
    UpdateExecutionRequest request)
        {
            _unitOfWork.BeginTransaction();

            try
            {
                var execution = await _unitOfWork
                    .GetRepository<ServiceExecution>()
                    .Query(q => q
                        .Include(x => x.Booking)
                        .Include(x => x.ServiceExecutionAttachments))
                    .FirstOrDefaultAsync(x => x.ExecutionId == executionId);

                if (execution == null)
                    throw new Exception("Execution not found.");

                if (execution.ExecutedByServiceStaffId != serviceStaffId)
                    throw new Exception("You are not allowed to update this execution.");

                execution.ExecutionSummary = request.ExecutionSummary;
                execution.ActionTaken = request.ActionTaken;
                execution.ResultNote = request.ResultNote;
                execution.UpdatedAt = DateTime.UtcNow;

                _unitOfWork
                    .GetRepository<ServiceExecution>()
                    .Update(execution);

                if (request.Attachments != null && request.Attachments.Any())
                {
                    foreach (var item in request.Attachments)
                    {
                        await _unitOfWork
                            .GetRepository<ServiceExecutionAttachment>()
                            .AddAsync(new ServiceExecutionAttachment
                            {
                                ServiceExecutionAttachmentId = Guid.NewGuid(),

                                ExecutionId = execution.ExecutionId,

                                FileUrl = item.FileUrl,

                                FileType = item.FileType,

                                Description = item.Description,

                                UploadedAt = DateTime.UtcNow
                            });
                    }
                }

                await _unitOfWork.SaveAsync();

                _unitOfWork.CommitTransaction();
            }
            catch
            {
                _unitOfWork.RollBack();
                throw;
            }
        }

        public async Task<ExecutionResultDto> GetExecutionResultAsync(Guid bookingId)
        {
            var execution = await _unitOfWork
                .GetRepository<ServiceExecution>()
                .Query(q => q
                    .Include(x => x.ExecutedByServiceStaff)
                    .Include(x => x.ServiceExecutionAttachments))
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (execution == null)
                throw new Exception("Execution result not found.");

            return new ExecutionResultDto
            {
                ExecutionId = execution.ExecutionId,

                BookingId = execution.BookingId,

                ExecutedBy = execution.ExecutedByServiceStaff.FullName,

                ExecutionSummary = execution.ExecutionSummary,

                ActionTaken = execution.ActionTaken,

                ResultNote = execution.ResultNote,

                Status = execution.Status,

                ExecutedAt = execution.ExecutedAt,

                Attachments = execution.ServiceExecutionAttachments
                    .OrderBy(x => x.UploadedAt)
                    .Select(x => new ExecutionAttachmentDto
                    {
                        AttachmentId = x.ServiceExecutionAttachmentId,

                        FileUrl = x.FileUrl,

                        FileType = x.FileType,

                        Description = x.Description,

                        UploadedAt = x.UploadedAt
                    })
                    .ToList()
            };
        }

        public async Task<ServiceReviewDto> ReviewBookingAsync(
    Guid userId,
    Guid bookingId,
    CreateServiceReviewRequest request)
        {
            var booking = await _unitOfWork
                .GetRepository<Booking>()
                .Query(q => q
                    .Include(x => x.ServiceReview))
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (booking == null)
                throw new Exception("Booking not found.");

            if (booking.UserId != userId)
                throw new Exception("You are not allowed to review this booking.");

            if (booking.Status != BookingStatus.Completed)
                throw new Exception("Booking has not been completed.");

            if (booking.ServiceReview != null)
                throw new Exception("Booking has already been reviewed.");

            var review = new ServiceReview
            {
                ReviewId = Guid.NewGuid(),

                BookingId = bookingId,

                UserId = userId,

                Rating = request.Rating,

                Comment = request.Comment,

                IsSatisfied = request.IsSatisfied,

                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork
                .GetRepository<ServiceReview>()
                .AddAsync(review);

            await _unitOfWork.SaveAsync();

            return new ServiceReviewDto
            {
                ReviewId = review.ReviewId,

                BookingId = bookingId,

                UserId = userId,

                Rating = review.Rating,

                Comment = review.Comment,

                IsSatisfied = review.IsSatisfied,

                CreatedAt = review.CreatedAt
            };
        }

        public async Task<ServiceReviewDto> GetBookingReviewAsync(Guid bookingId)
        {
            var review = await _unitOfWork
                .GetRepository<ServiceReview>()
                .Query(q => q.Include(x => x.User))
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            if (review == null)
                throw new Exception("Review not found.");

            return new ServiceReviewDto
            {
                ReviewId = review.ReviewId,

                BookingId = review.BookingId,

                UserId = review.UserId,

                UserName = review.User.FullName,

                Rating = review.Rating,

                Comment = review.Comment,

                IsSatisfied = review.IsSatisfied,

                CreatedAt = review.CreatedAt
            };
        }

        
    }
}
