using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Dtos;
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

    [HttpGet]
    public async Task<IActionResult> GetMyFeedbacks([FromQuery] FeedbackQueryParameters query)
    {
        var result = await _feedbackService.GetMyFeedbacksAsync(GetCurrentUserId(), query);
        return Ok(result);
    }

    [HttpGet("{feedbackId:guid}")]
    public async Task<IActionResult> GetMyFeedbackDetail(Guid feedbackId)
    {
        var result = await _feedbackService.GetMyFeedbackDetailAsync(GetCurrentUserId(), feedbackId);
        return Ok(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateFeedback([FromForm] FeedbackCreateFormRequest form)
    {
        var attachments = await UploadFilesAsync(form.Attachments, "urban-service/feedbacks");
        var request = new FeedbackCreateRequest
        {
            CategoryId = form.CategoryId,
            Title = form.Title,
            Description = form.Description,
            LocationText = form.LocationText,
            Latitude = form.Latitude,
            Longitude = form.Longitude,
            Priority = form.Priority,
            DueDate = form.DueDate
        };

        var result = await _feedbackService.CreateAsync(GetCurrentUserId(), request, attachments);
        return Ok(result);
    }

    [HttpPut("{feedbackId:guid}")]
    public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromBody] FeedbackUpdateRequest request)
    {
        var result = await _feedbackService.UpdateAsync(GetCurrentUserId(), feedbackId, request);
        return Ok(result);
    }

    [HttpDelete("{feedbackId:guid}")]
    public async Task<IActionResult> DeleteFeedback(Guid feedbackId)
    {
        await _feedbackService.DeleteAsync(GetCurrentUserId(), feedbackId);
        return NoContent();
    }

    [HttpPost("{feedbackId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddAttachments(Guid feedbackId, [FromForm] FeedbackAttachmentUploadRequest form)
    {
        var attachments = await UploadFilesAsync(form.Files, "urban-service/feedbacks");
        var result = await _feedbackService.AddAttachmentsAsync(GetCurrentUserId(), feedbackId, attachments);
        return Ok(result);
    }

    [HttpDelete("{feedbackId:guid}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DeleteAttachment(Guid feedbackId, int attachmentId)
    {
        await _feedbackService.DeleteAttachmentAsync(GetCurrentUserId(), feedbackId, attachmentId);
        return NoContent();
    }

    [HttpPatch("{feedbackId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid feedbackId, [FromBody] UpdateFeedbackStatusRequest request)
    {
        var result = await _feedbackService.UpdateStatusAsync(GetCurrentUserId(), feedbackId, request);
        return Ok(result);
    }

    [HttpPost("{feedbackId:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid feedbackId, [FromBody] FeedbackCommentCreateRequest request)
    {
        var result = await _feedbackService.AddCommentAsync(GetCurrentUserId(), feedbackId, request);
        return Ok(result);
    }

    [HttpPost("{feedbackId:guid}/support")]
    public async Task<IActionResult> Support(Guid feedbackId)
    {
        await _feedbackService.SupportAsync(GetCurrentUserId(), feedbackId);
        return NoContent();
    }

    [HttpDelete("{feedbackId:guid}/support")]
    public async Task<IActionResult> Unsupport(Guid feedbackId)
    {
        await _feedbackService.UnsupportAsync(GetCurrentUserId(), feedbackId);
        return NoContent();
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
    public int CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string LocationText { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public List<IFormFile>? Attachments { get; set; }
}

public class FeedbackAttachmentUploadRequest
{
    public List<IFormFile>? Files { get; set; }
}
