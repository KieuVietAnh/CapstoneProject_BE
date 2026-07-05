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
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/management/provider-reports")]
public class ManagementProviderReportsController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly ICloudinaryService _cloudinaryService;

    public ManagementProviderReportsController(
        IFeedbackService feedbackService,
        ICloudinaryService cloudinaryService)
    {
        _feedbackService = feedbackService;
        _cloudinaryService = cloudinaryService;
    }

    /// <summary>Cap nhat trang thai report gui Service Provider.</summary>
    [HttpPatch("{providerReportId:int}/status")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF + "," + UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(FeedbackProviderReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(
        int providerReportId,
        [FromBody] UpdateProviderReportStatusRequest request)
    {
        var result = await _feedbackService.UpdateProviderReportStatusAsync(
            providerReportId,
            GetCurrentUserId(),
            request);

        return Ok(result);
    }

    /// <summary>Xem lich su lien he Service Provider cua mot provider report.</summary>
    [HttpGet("{providerReportId:int}/contact-logs")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderContactLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContactLogs(int providerReportId)
    {
        var result = await _feedbackService.GetProviderContactLogsAsync(providerReportId);
        return Ok(result);
    }

    /// <summary>Staff ghi lai ket qua da goi/email/nhan tin Service Provider.</summary>
    [HttpPost("{providerReportId:int}/contact-logs")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(ProviderContactLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddContactLog(
        int providerReportId,
        [FromBody] ProviderContactLogCreateRequest request)
    {
        var result = await _feedbackService.AddProviderContactLogAsync(
            providerReportId,
            GetCurrentUserId(),
            request);

        return Ok(result);
    }

    /// <summary>Xem tai lieu/anh hoan thanh cua mot provider report.</summary>
    [HttpGet("{providerReportId:int}/completion-documents")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompletionDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCompletionDocuments(int providerReportId)
    {
        var result = await _feedbackService.GetCompletionDocumentsAsync(providerReportId);
        return Ok(result);
    }

    /// <summary>Upload anh/tai lieu hoan thanh cho provider report.</summary>
    [HttpPost("{providerReportId:int}/completion-documents")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompletionDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddCompletionDocuments(
        int providerReportId,
        [FromForm] CompletionDocumentUploadRequest form)
    {
        var documents = await UploadFilesAsync(form.Files, "urban-service/completion-documents");
        var result = await _feedbackService.AddCompletionDocumentsAsync(
            providerReportId,
            GetCurrentUserId(),
            documents,
            form.Description);

        return Ok(result);
    }

    private async Task<IReadOnlyCollection<UploadedFeedbackAttachmentDto>> UploadFilesAsync(
        IReadOnlyCollection<IFormFile>? files,
        string folder)
    {
        if (files == null || files.Count == 0)
        {
            throw new Exception("Files la bat buoc.");
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

public class CompletionDocumentUploadRequest
{
    public string? Description { get; set; }

    public List<IFormFile>? Files { get; set; }
}
