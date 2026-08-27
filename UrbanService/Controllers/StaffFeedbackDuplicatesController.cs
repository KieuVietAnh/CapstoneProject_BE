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
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/staff/feedback-duplicates")]
[Route("api/management/incident-match-candidates")]
public class StaffFeedbackDuplicatesController : ControllerBase
{
    private readonly IFeedbackDuplicateCandidateService _duplicateCandidateService;

    public StaffFeedbackDuplicatesController(IFeedbackDuplicateCandidateService duplicateCandidateService)
    {
        _duplicateCandidateService = duplicateCandidateService;
    }

    /// <summary>Lấy số lượng đề xuất các Report có thể cùng thuộc một Incident.</summary>
    /// <remarks>
    /// Manager xem theo các phường phụ trách; Admin chỉ xem để kiểm tra/audit.
    /// </remarks>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(FeedbackDuplicateSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary()
    {
        var result = await _duplicateCandidateService.GetSummaryAsync(GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>Lấy danh sách đề xuất các Report có thể cùng thuộc một Incident.</summary>
    /// <remarks>
    /// Manager xem theo các phường phụ trách; Admin chỉ xem để kiểm tra/audit.
    /// Hỗ trợ filter theo status, ví dụ Pending/Confirmed/Rejected, và phân trang.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<FeedbackDuplicateCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCandidates([FromQuery] FeedbackDuplicateQueryParameters query)
    {
        var result = await _duplicateCandidateService.GetCandidatesAsync(query, GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>So sánh Report mới với Report đại diện của Incident được đề xuất.</summary>
    /// <remarks>
    /// Manager xem theo các phường phụ trách; Admin chỉ xem để kiểm tra/audit.
    /// FE dùng response này để render màn compare feedback mới và potential parent feedback.
    /// </remarks>
    [HttpGet("{duplicateCandidateId:guid}")]
    [ProducesResponseType(typeof(FeedbackDuplicateCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCandidateDetail(Guid duplicateCandidateId)
    {
        var result = await _duplicateCandidateService.GetCandidateDetailAsync(
            duplicateCandidateId,
            GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>Xác nhận hai Report cùng sự vụ và chuyển Report vào Incident canonical.</summary>
    /// <remarks>
    /// Chỉ Manager phụ trách phường của cả hai Incident được phép xác nhận.
    /// Khi confirm, Feedback/Report vẫn được giữ nguyên; active link được chuyển sang
    /// Incident canonical, Incident rỗng được đánh dấu Merged và candidate chuyển Confirmed.
    /// ParentTicketId/IsMasterTicket vẫn được cập nhật tạm thời để tương thích API cũ.
    /// </remarks>
    [HttpPost("{duplicateCandidateId:guid}/confirm")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(FeedbackDuplicateCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Confirm(Guid duplicateCandidateId)
    {
        var result = await _duplicateCandidateService.ConfirmAsync(duplicateCandidateId, GetCurrentUserId());
        return Ok(result);
    }

    /// <summary>Từ chối đề xuất cùng sự vụ; mỗi Report tiếp tục thuộc Incident riêng.</summary>
    /// <remarks>
    /// Chỉ Manager phụ trách phường của Incident được phép từ chối.
    /// Khi reject, candidate chuyển Rejected, không relink Report và không merge Incident.
    /// </remarks>
    [HttpPost("{duplicateCandidateId:guid}/reject")]
    [Authorize(Roles = UserRole.INTERACTIONMANAGER)]
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
