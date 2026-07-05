using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SERVICEUSER)]
[Route("api/user/feedbacks")]
public class UserFeedbacksController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly ICloudinaryService _cloudinaryService;

    public UserFeedbacksController(IFeedbackService feedbackService, ICloudinaryService cloudinaryService)
    {
        _feedbackService = feedbackService;
        _cloudinaryService = cloudinaryService;
    }

    /// <summary>Xem danh sách feedback do người dân hiện tại tạo.</summary>
    /// <remarks>
    /// Yêu cầu role `SERVICEUSER`. Hỗ trợ phân trang và lọc theo `status`,
    /// `categoryId`, `search`.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<FeedbackListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyFeedbacks([FromQuery] FeedbackQueryParameters query)
    {
        var result = await _feedbackService.GetMyFeedbacksAsync(GetCurrentUserId(), query);
        return Ok(result);
    }

    /// <summary>Xem bang tin feedback cua nguoi dan.</summary>
    /// <remarks>
    /// Yeu cau role `SERVICEUSER`. Tra ve tat ca feedback da qua buoc noi bo,
    /// loai cac feedback dang `Submitted` hoac `AiReviewed`.
    /// Ho tro phan trang va loc theo `status`, `categoryId`, `search`.
    /// </remarks>
    [HttpGet("feed")]
    [ProducesResponseType(typeof(PagedResultDto<FeedbackListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetResidentFeedFeedbacks([FromQuery] FeedbackQueryParameters query)
    {
        var result = await _feedbackService.GetResidentFeedFeedbacksAsync(query);
        return Ok(result);
    }

    /// <summary>Xem chi tiết một feedback của người dân hiện tại.</summary>
    /// <remarks>Chỉ chủ sở hữu feedback có role `SERVICEUSER` được xem.</remarks>
    [HttpGet("{feedbackId:guid}")]
    [ProducesResponseType(typeof(FeedbackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyFeedbackDetail(Guid feedbackId)
    {
        var result = await _feedbackService.GetMyFeedbackDetailAsync(GetCurrentUserId(), feedbackId);
        return Ok(result);
    }

    /// <summary>
    /// Người dân tạo một feedback mới, có thể đính kèm file.
    /// </summary>
    /// <remarks>
    /// Yêu cầu tài khoản có role `SERVICEUSER` và JWT hợp lệ.
    ///
    /// Gửi dữ liệu dưới dạng `multipart/form-data`. Các trường bắt buộc gồm
    /// `categoryId`, `title`, `description` và `locationText`.
    ///
    /// Quy trình xử lý:
    /// 1. File trong `attachments` được tải lên Cloudinary.
    /// 2. Hệ thống kiểm tra `categoryId` tồn tại và đang hoạt động.
    /// 3. Feedback được tạo với `status = Submitted`.
    /// 4. Nếu không truyền `priority`, hệ thống dùng `Medium`.
    /// 5. Hệ thống tạo lịch sử trạng thái đầu tiên với ghi chú `Feedback created`.
    ///
    /// Hiện tại hệ thống chưa có API công khai để lấy danh sách category; client
    /// cần sử dụng một `categoryId` hợp lệ đã được cung cấp.
    /// </remarks>
    /// <response code="200">Tạo feedback thành công, trả về toàn bộ chi tiết feedback.</response>
    /// <response code="400">Dữ liệu thiếu/không hợp lệ, category không tồn tại hoặc upload file thất bại.</response>
    /// <response code="401">JWT thiếu, hết hạn hoặc không hợp lệ.</response>
    /// <response code="403">Tài khoản không có role SERVICEUSER.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FeedbackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateFeedback([FromForm] FeedbackCreateFormRequest form)
    {
        var attachments = await UploadFilesAsync(form.Attachments, "urban-service/feedbacks");
        var request = new FeedbackCreateRequest
        {
            AreaId = form.AreaId,
            CategoryId = form.CategoryId,
            Title = form.Title,
            Description = form.Description,
            LocationText = form.LocationText,
            Latitude = form.Latitude,
            Longitude = form.Longitude,
            LocationAccuracyMeters = form.LocationAccuracyMeters,
            GeoSource = form.GeoSource,
            Priority = form.Priority,
            DueDate = form.DueDate
        };

        var result = await _feedbackService.CreateAsync(GetCurrentUserId(), request, attachments);
        return Ok(result);
    }

    /// <summary>Chỉnh sửa feedback của người dân hiện tại.</summary>
    /// <remarks>
    /// Yêu cầu role `SERVICEUSER` và phải là chủ sở hữu feedback. Chỉ các trường
    /// được truyền trong body mới được cập nhật.
    /// </remarks>
    [HttpPut("{feedbackId:guid}")]
    [ProducesResponseType(typeof(FeedbackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] FeedbackUpdateRequest request)
    {
        var result = await _feedbackService.UpdateAsync(GetCurrentUserId(), feedbackId, request);
        return Ok(result);
    }

    /// <summary>Xóa feedback của người dân hiện tại.</summary>
    /// <remarks>Yêu cầu role `SERVICEUSER` và phải là chủ sở hữu feedback.</remarks>
    [HttpDelete("{feedbackId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteFeedback(Guid feedbackId)
    {
        await _feedbackService.DeleteAsync(GetCurrentUserId(), feedbackId);
        return NoContent();
    }

    /// <summary>Thêm file đính kèm vào feedback.</summary>
    /// <remarks>
    /// Yêu cầu role `SERVICEUSER`, phải là chủ sở hữu feedback và gửi file bằng
    /// `multipart/form-data`. File được tải lên Cloudinary.
    /// </remarks>
    [HttpPost("{feedbackId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FeedbackDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddAttachments(Guid feedbackId, [FromForm] FeedbackAttachmentUploadRequest form)
    {
        var attachments = await UploadFilesAsync(form.Files, "urban-service/feedbacks");
        var result = await _feedbackService.AddAttachmentsAsync(GetCurrentUserId(), feedbackId, attachments);
        return Ok(result);
    }

    /// <summary>Xóa một file đính kèm khỏi feedback.</summary>
    /// <remarks>Yêu cầu role `SERVICEUSER` và phải là chủ sở hữu feedback.</remarks>
    [HttpDelete("{feedbackId:guid}/attachments/{attachmentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAttachment(Guid feedbackId, int attachmentId)
    {
        await _feedbackService.DeleteAttachmentAsync(GetCurrentUserId(), feedbackId, attachmentId);
        return NoContent();
    }

    /// <summary>Thêm bình luận vào một feedback.</summary>
    /// <remarks>
    /// Yêu cầu role `SERVICEUSER`. Có thể bình luận vào feedback tồn tại, không
    /// bắt buộc là chủ sở hữu.
    /// </remarks>
    [HttpPost("{feedbackId:guid}/comments")]
    [ProducesResponseType(typeof(FeedbackCommentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddComment(Guid feedbackId, [FromBody] FeedbackCommentCreateRequest request)
    {
        var result = await _feedbackService.AddCommentAsync(GetCurrentUserId(), feedbackId, request);
        return Ok(result);
    }

    /// <summary>Đồng tình với một feedback.</summary>
    /// <remarks>
    /// Yêu cầu role `SERVICEUSER`. Gọi lại nhiều lần không tạo bản ghi trùng.
    /// </remarks>
    [HttpPost("{feedbackId:guid}/support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Support(Guid feedbackId)
    {
        await _feedbackService.SupportAsync(GetCurrentUserId(), feedbackId);
        return NoContent();
    }

    /// <summary>Hủy đồng tình với một feedback.</summary>
    /// <remarks>Yêu cầu role `SERVICEUSER`. Nếu chưa đồng tình, API vẫn trả về thành công.</remarks>
    [HttpDelete("{feedbackId:guid}/support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Unsupport(Guid feedbackId)
    {
        await _feedbackService.UnsupportAsync(GetCurrentUserId(), feedbackId);
        return NoContent();
    }

    /// <summary>Nguoi dan danh gia ket qua xu ly sau khi feedback duoc duyet.</summary>
    [HttpPost("{feedbackId:guid}/resolution-review")]
    [ProducesResponseType(typeof(FeedbackResolutionReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReviewResolution(
        Guid feedbackId,
        [FromBody] CitizenResolutionReviewRequest request)
    {
        var result = await _feedbackService.CitizenReviewAsync(new CitizenReviewRequest
        {
            FeedbackId = feedbackId,
            UserId = GetCurrentUserId(),
            Rating = request.Rating,
            IsSatisfied = request.IsSatisfied,
            Comment = request.Comment
        });

        return Ok(result);
    }

    private async Task<IReadOnlyCollection<UploadedFeedbackAttachmentDto>> UploadFilesAsync(
        IReadOnlyCollection<IFormFile>? files,
        string folder)
    {
        if (files == null || files.Count == 0)
        {
            return [];
        }

        var attachments = new List<UploadedFeedbackAttachmentDto>();

        foreach (var file in files.Where(f => f.Length > 0))
        {
            await using var stream = file.OpenReadStream();
            var uploadResult = await _cloudinaryService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                folder,
                HttpContext.RequestAborted);

            attachments.Add(new UploadedFeedbackAttachmentDto
            {
                FileUrl = uploadResult.FileUrl,
                FileType = uploadResult.FileType
            });
        }

        return attachments;
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new UnauthorizedAccessException();
        }

        return parsedUserId;
    }
}

public class FeedbackCreateFormRequest
{
    /// <summary>ID cua khu vuc van hanh.</summary>
    /// <example>1</example>
    public int AreaId { get; set; }

    /// <summary>ID của danh mục dịch vụ đô thị đang hoạt động.</summary>
    /// <example>1</example>
    public int CategoryId { get; set; }

    /// <summary>Tiêu đề ngắn mô tả vấn đề.</summary>
    /// <example>Đèn đường không hoạt động</example>
    public string Title { get; set; } = null!;

    /// <summary>Mô tả chi tiết vấn đề người dân muốn phản ánh.</summary>
    /// <example>Đèn đường trước số 12 đã tắt ba ngày liên tiếp.</example>
    public string Description { get; set; } = null!;

    /// <summary>Địa chỉ hoặc mô tả vị trí xảy ra vấn đề.</summary>
    /// <example>12 Nguyễn Huệ, Quận 1</example>
    public string LocationText { get; set; } = null!;

    /// <summary>Vĩ độ của vị trí, nếu có.</summary>
    /// <example>10.7731</example>
    public decimal? Latitude { get; set; }

    /// <summary>Kinh độ của vị trí, nếu có.</summary>
    /// <example>106.7043</example>
    public decimal? Longitude { get; set; }

    /// <summary>Do chinh xac vi tri tinh bang met, neu co.</summary>
    public int? LocationAccuracyMeters { get; set; }

    /// <summary>Nguon lay toa do, vi du GPS hoac Manual.</summary>
    public string? GeoSource { get; set; }

    /// <summary>Mức độ ưu tiên. Mặc định là Medium nếu bỏ trống.</summary>
    /// <example>Medium</example>
    public string? Priority { get; set; }

    /// <summary>Thời hạn mong muốn xử lý, nếu có. Dùng định dạng ISO 8601.</summary>
    /// <example>2026-06-15T10:00:00Z</example>
    public DateTime? DueDate { get; set; }

    /// <summary>Danh sách file ảnh hoặc tài liệu đính kèm, nếu có.</summary>
    public List<IFormFile>? Attachments { get; set; }
}

public class FeedbackAttachmentUploadRequest
{
    /// <summary>Danh sách file cần thêm vào feedback.</summary>
    public List<IFormFile>? Files { get; set; }
}
