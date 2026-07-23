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
[Authorize(Roles = UserRole.SYSTEMSTAFF + "," + UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/staff/feedback-duplicates")]
public class StaffFeedbackDuplicatesController : ControllerBase
{
    private readonly IFeedbackDuplicateCandidateService _duplicateCandidateService;

    public StaffFeedbackDuplicatesController(IFeedbackDuplicateCandidateService duplicateCandidateService)
    {
        _duplicateCandidateService = duplicateCandidateService;
    }

    /// <summary>Lấy số lượng case feedback nghi trùng theo trạng thái.</summary>
    /// <remarks>
    /// Role được phép: SYSTEMSTAFF, SYSTEMADMIN, INTERACTIONMANAGER.
    /// FE dùng để hiển thị badge/card pending count trên staff dashboard.
    /// </remarks>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(FeedbackDuplicateSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _duplicateCandidateService.GetSummaryAsync();
        return Ok(result);
    }

    /// <summary>Lấy danh sách case feedback nghi trùng.</summary>
    /// <remarks>
    /// Role được phép: SYSTEMSTAFF, SYSTEMADMIN, INTERACTIONMANAGER.
    /// Hỗ trợ filter theo status, ví dụ Pending/Confirmed/Rejected, và phân trang.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<FeedbackDuplicateCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCandidates([FromQuery] FeedbackDuplicateQueryParameters query)
    {
        var result = await _duplicateCandidateService.GetCandidatesAsync(query);
        return Ok(result);
    }

    /// <summary>Xem chi tiết một case feedback nghi trùng.</summary>
    /// <remarks>
    /// Role được phép: SYSTEMSTAFF, SYSTEMADMIN, INTERACTIONMANAGER.
    /// FE dùng response này để render màn compare feedback mới và potential parent feedback.
    /// </remarks>
    [HttpGet("{duplicateCandidateId:guid}")]
    [ProducesResponseType(typeof(FeedbackDuplicateCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCandidateDetail(Guid duplicateCandidateId)
    {
        var result = await _duplicateCandidateService.GetCandidateDetailAsync(duplicateCandidateId);
        return Ok(result);
    }

    /// <summary>Staff xác nhận hai feedback là trùng nhau và thực hiện gộp ticket.</summary>
    /// <remarks>
    /// Chỉ role SYSTEMSTAFF/SYSTEMADMIN/INTERACTIONMANAGER được phép.
    /// Khi confirm:
    /// - feedback con được set ParentTicketId = parentFeedbackId
    /// - feedback con IsMasterTicket = false
    /// - feedback chính IsMasterTicket = true
    /// - duplicate candidate chuyển Confirmed
    /// </remarks>
    [HttpPost("{duplicateCandidateId:guid}/confirm")]
    [ProducesResponseType(typeof(FeedbackDuplicateCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Confirm(Guid duplicateCandidateId)
    {
        var result = await _duplicateCandidateService.ConfirmAsync(duplicateCandidateId, GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>Staff từ chối case nghi trùng, không gộp ticket.</summary>
    /// <remarks>
    /// Chỉ role SYSTEMSTAFF/SYSTEMADMIN/INTERACTIONMANAGER được phép.
    /// Khi reject, hệ thống chỉ chuyển duplicate candidate sang Rejected và không set ParentTicketId.
    /// </remarks>
    [HttpPost("{duplicateCandidateId:guid}/reject")]
    [ProducesResponseType(typeof(FeedbackDuplicateCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Reject(Guid duplicateCandidateId)
    {
        var result = await _duplicateCandidateService.RejectAsync(duplicateCandidateId, GetCurrentUserId());
        return Ok(result);
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