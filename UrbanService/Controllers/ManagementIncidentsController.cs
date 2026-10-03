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
[Route("api/management/incidents")]
public sealed class ManagementIncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;
    private readonly IFeedbackService _feedbackService;
    private readonly ICloudinaryService _cloudinaryService;

    public ManagementIncidentsController(
        IIncidentService incidentService,
        IFeedbackService feedbackService,
        ICloudinaryService cloudinaryService)
    {
        _incidentService = incidentService;
        _feedbackService = feedbackService;
        _cloudinaryService = cloudinaryService;
    }

    /// <summary>Lấy queue Incident cho management.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<IncidentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidents([FromQuery] IncidentQueryParameters query)
    {
        return Ok(await _incidentService.GetIncidentsAsync(
            query,
            GetCurrentUserId(),
            HttpContext.RequestAborted));
    }

    /// <summary>Lấy chi tiết Incident, các Report, subscriber và event timeline.</summary>
    [HttpGet("{incidentId:guid}")]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIncident(Guid incidentId)
    {
        return Ok(await _incidentService.GetIncidentDetailAsync(
            incidentId,
            GetCurrentUserId(),
            HttpContext.RequestAborted));
    }

    /// <summary>Liên kết một Feedback/Report chưa có active link vào Incident.</summary>
    [HttpPost("{incidentId:guid}/reports")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkReport(Guid incidentId, [FromBody] LinkIncidentReportRequest request)
    {
        return Ok(await _incidentService.LinkReportAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));
    }

    /// <summary>Soft-unlink một Feedback/Report khỏi Incident và giữ audit history.</summary>
    [HttpDelete("{incidentId:guid}/reports/{feedbackId:guid}")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlinkReport(Guid incidentId, Guid feedbackId)
    {
        await _incidentService.UnlinkReportAsync(
            incidentId,
            feedbackId,
            GetCurrentUserId(),
            HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>Cập nhật dữ liệu điều phối của Incident, gồm Severity.</summary>
    [HttpPatch("{incidentId:guid}")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateIncident(Guid incidentId, [FromBody] UpdateIncidentRequest request)
        => Ok(await _incidentService.UpdateIncidentAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Chuyển trạng thái xử lý ở cấp Incident.</summary>
    [HttpPatch("{incidentId:guid}/status")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(Guid incidentId, [FromBody] UpdateIncidentStatusRequest request)
        => Ok(await _incidentService.UpdateStatusAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Lấy Staff phù hợp khu vực và danh mục của Incident.</summary>
    [HttpGet("{incidentId:guid}/assignee-candidates")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IReadOnlyCollection<IncidentAssigneeCandidateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssigneeCandidates(Guid incidentId)
        => Ok(await _incidentService.GetAssigneeCandidatesAsync(
            incidentId,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Phân công Staff xử lý Incident.</summary>
    [HttpPost("{incidentId:guid}/assign")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(Guid incidentId, [FromBody] AssignIncidentRequest request)
        => Ok(await _incidentService.AssignAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Staff xác nhận tự xử lý Incident, không qua đơn vị bên thứ ba.</summary>
    /// <remarks>
    /// Chỉ Staff đang được phân công, và chỉ khi Incident ở trạng thái `Assigned`
    /// và chưa có đơn vị xử lý nào. Chuyển Incident sang `InProgress`.
    ///
    /// Không dùng endpoint trạng thái chung vì endpoint đó chỉ dành cho Manager và
    /// chỉ nhận `Rejected` hoặc `Cancelled`.
    /// </remarks>
    [HttpPost("{incidentId:guid}/start-processing")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartProcessing(
        Guid incidentId,
        [FromBody] StartIncidentProcessingRequest? request)
        => Ok(await _incidentService.StartDirectProcessingAsync(
            incidentId,
            GetCurrentUserId(),
            request?.Note,
            HttpContext.RequestAborted));

    /// <summary>Minh chứng xử lý của Incident do Staff tự xử lý.</summary>
    /// <remarks>Chỉ áp dụng cho sự vụ chưa có đơn vị bên thứ ba.</remarks>
    [HttpGet("{incidentId:guid}/completion-documents")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompletionDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidentCompletionDocuments(Guid incidentId)
        => Ok(await _feedbackService.GetIncidentCompletionDocumentsAsync(
            incidentId,
            GetCurrentUserId()));

    /// <summary>Staff tải minh chứng cho Incident mình tự xử lý.</summary>
    /// <remarks>
    /// Dùng khi sự vụ không qua đơn vị bên thứ ba. Sự vụ đã có đơn vị thì minh chứng
    /// phải đi qua endpoint của phân công để còn biết ảnh thuộc trách nhiệm của ai.
    /// </remarks>
    [HttpPost("{incidentId:guid}/completion-documents")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CompletionDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddIncidentCompletionDocuments(
        Guid incidentId,
        [FromForm] IncidentCompletionDocumentUploadRequest form)
    {
        var documents = await UploadIncidentFilesAsync(form.Files, "urban-service/completion-documents");
        return Ok(await _feedbackService.AddIncidentCompletionDocumentsAsync(
            incidentId,
            GetCurrentUserId(),
            documents,
            form.Description));
    }

    /// <summary>Xóa toàn bộ minh chứng cũ của Incident tự xử lý.</summary>
    /// <remarks>Chỉ dùng khi sự vụ đang ở trạng thái cần xử lý lại.</remarks>
    [HttpDelete("{incidentId:guid}/completion-documents")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearIncidentCompletionDocuments(Guid incidentId)
    {
        await _feedbackService.ClearIncidentCompletionDocumentsAsync(
            incidentId,
            GetCurrentUserId());
        return NoContent();
    }

    /// <summary>Lấy đơn vị xử lý phù hợp với khu vực và danh mục của Incident.</summary>
    [HttpGet("{incidentId:guid}/provider-candidates")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderCandidateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviderCandidates(Guid incidentId)
        => Ok(await _feedbackService.GetIncidentProviderCandidatesAsync(
            incidentId,
            GetCurrentUserId()));

    /// <summary>Lấy phân công đơn vị xử lý hiện tại của Incident.</summary>
    [HttpGet("{incidentId:guid}/provider-assignment")]
    [ProducesResponseType(typeof(IncidentProviderAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetProviderAssignment(Guid incidentId)
    {
        var assignment = await _feedbackService.GetCurrentProviderAssignmentAsync(
            incidentId,
            GetCurrentUserId());
        return assignment == null ? NoContent() : Ok(assignment);
    }

    /// <summary>Staff phân công một đơn vị xử lý cho Incident.</summary>
    /// <remarks>Mỗi Incident chỉ có một phân công và không hỗ trợ đổi đơn vị.</remarks>
    [HttpPost("{incidentId:guid}/provider-assignment")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(IncidentProviderAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignProvider(
        Guid incidentId,
        [FromBody] AssignIncidentProviderRequest request)
    {
        var assignment = await _feedbackService.AssignIncidentProviderAsync(
            incidentId,
            GetCurrentUserId(),
            request);
        return CreatedAtAction(
            nameof(GetProviderAssignment),
            new { incidentId },
            assignment);
    }

    /// <summary>Lấy kết quả xử lý đã gửi cho Incident.</summary>
    [HttpGet("{incidentId:guid}/resolutions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<FeedbackResolutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResolutions(Guid incidentId)
        => Ok(await _feedbackService.GetIncidentResolutionsAsync(
            incidentId,
            GetCurrentUserId()));

    /// <summary>Lấy kết quả xử lý hiện tại của Incident.</summary>
    [HttpGet("{incidentId:guid}/resolution")]
    [HttpGet("{incidentId:guid}/resolutions/latest")]
    [ProducesResponseType(typeof(FeedbackResolutionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentResolution(Guid incidentId)
        => Ok(await _feedbackService.GetCurrentIncidentResolutionAsync(
            incidentId,
            GetCurrentUserId()));

    /// <summary>Lấy đánh giá của người dân về kết quả xử lý Incident.</summary>
    /// <remarks>
    /// Một Incident gộp nhiều phản ánh của nhiều người, mỗi người đánh giá phần phản
    /// ánh của mình, nên endpoint trả về danh sách chứ không phải một đánh giá.
    ///
    /// Kèm vài số liệu tổng hợp: số phản ánh đang gộp (tức số người có quyền đánh
    /// giá), số đánh giá đã nhận, số người hài lòng và điểm trung bình.
    /// </remarks>
    [HttpGet("{incidentId:guid}/resolution-reviews")]
    [ProducesResponseType(typeof(IncidentResolutionReviewSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetResolutionReviews(Guid incidentId)
        => Ok(await _feedbackService.GetIncidentResolutionReviewsAsync(
            incidentId,
            GetCurrentUserId()));

    /// <summary>Staff gửi kết quả xử lý của Incident để Manager duyệt.</summary>
    [HttpPost("{incidentId:guid}/resolutions")]
    [Authorize(Roles = UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitResolution(
        Guid incidentId,
        [FromBody] SubmitResolutionRequest request)
    {
        var resolution = await _feedbackService.SubmitIncidentResolutionAsync(
            incidentId,
            GetCurrentUserId(),
            request);
        resolution.Message = "Resolution submitted successfully.";
        return Ok(resolution);
    }

    /// <summary>Manager phê duyệt kết quả xử lý hiện tại của Incident.</summary>
    [HttpPost("{incidentId:guid}/resolutions/{resolutionId:int}/approve")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackResolutionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveResolution(
        Guid incidentId,
        int resolutionId,
        [FromQuery] string? note)
        => Ok(await _feedbackService.ApproveIncidentResolutionAsync(
            incidentId,
            resolutionId,
            GetCurrentUserId(),
            note));

    /// <summary>Manager yêu cầu làm lại kết quả xử lý hiện tại của Incident.</summary>
    [HttpPost("{incidentId:guid}/resolutions/{resolutionId:int}/need-rework")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackResolutionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> NeedRework(
        Guid incidentId,
        int resolutionId,
        [FromBody] NeedReworkResolutionRequest request)
        => Ok(await _feedbackService.RequireIncidentResolutionReworkAsync(
            incidentId,
            resolutionId,
            GetCurrentUserId(),
            request.Reason));

    /// <summary>Merge Incident nguồn vào Incident đích.</summary>
    [HttpPost("{incidentId:guid}/merge")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(IncidentDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Merge(Guid incidentId, [FromBody] MergeIncidentRequest request)
        => Ok(await _incidentService.MergeAsync(
            incidentId,
            request,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    /// <summary>Lấy timeline Incident có phân trang.</summary>
    [HttpGet("{incidentId:guid}/timeline")]
    [ProducesResponseType(typeof(PagedResultDto<IncidentEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimeline(
        Guid incidentId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _incidentService.GetManagementTimelineAsync(
            incidentId,
            pageNumber,
            pageSize,
            GetCurrentUserId(),
            HttpContext.RequestAborted));

    private async Task<IReadOnlyCollection<UploadedFeedbackAttachmentDto>> UploadIncidentFilesAsync(
        IReadOnlyCollection<IFormFile>? files,
        string folder)
    {
        if (files == null || files.Count == 0)
        {
            throw new Exception("Files la bat buoc.");
        }

        var attachments = new List<UploadedFeedbackAttachmentDto>();

        foreach (var file in files.Where(item => item.Length > 0))
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

/// <summary>Minh chứng tải lên cho sự vụ Staff tự xử lý.</summary>
public class IncidentCompletionDocumentUploadRequest
{
    public string? Description { get; set; }

    public List<IFormFile>? Files { get; set; }
}
