using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/management/staff-area-assignments")]
public class ManagementStaffAreaAssignmentsController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public ManagementStaffAreaAssignmentsController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<StaffAreaAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] Guid? userId = null,
        [FromQuery] int? areaId = null,
        [FromQuery] bool? isActive = null)
    {
        var query = GetAssignmentQuery().AsNoTracking();

        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (areaId.HasValue)
        {
            query = query.Where(a => a.AreaId == areaId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        var assignments = await query
            .OrderByDescending(a => a.IsActive)
            .ThenBy(a => a.Area.AreaName)
            .ThenBy(a => a.User.FullName)
            .ToListAsync();

        return Ok(assignments.Select(Map).ToList());
    }

    [HttpPost]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(StaffAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] StaffAreaAssignmentCreateRequest request)
    {
        await EnsureStaffExistsAsync(request.UserId);
        await EnsureAreaExistsAsync(request.AreaId);

        var existing = await _uow.GetRepository<StaffAreaAssignment>().Entities
            .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.AreaId == request.AreaId);

        if (existing != null)
        {
            existing.IsActive = true;
            existing.IsPrimary = request.IsPrimary;
            existing.StartDate = request.StartDate;
            existing.EndDate = request.EndDate;
            await _uow.SaveAsync();

            return Ok(await GetAssignmentDtoAsync(existing.StaffAreaAssignmentId));
        }

        var assignment = new StaffAreaAssignment
        {
            UserId = request.UserId,
            AreaId = request.AreaId,
            AssignedByUserId = GetCurrentUserId(),
            IsPrimary = request.IsPrimary,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<StaffAreaAssignment>().AddAsync(assignment);
        await _uow.SaveAsync();

        return Ok(await GetAssignmentDtoAsync(assignment.StaffAreaAssignmentId));
    }

    [HttpPatch("{assignmentId:int}/active")]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(StaffAreaAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        int assignmentId,
        [FromBody] SetActiveRequest request)
    {
        var assignment = await _uow.GetRepository<StaffAreaAssignment>().GetByIdAsync(assignmentId)
            ?? throw new Exception("Assignment khong ton tai.");

        assignment.IsActive = request.IsActive;
        await _uow.SaveAsync();

        return Ok(await GetAssignmentDtoAsync(assignmentId));
    }

    private IQueryable<StaffAreaAssignment> GetAssignmentQuery()
    {
        return _uow.GetRepository<StaffAreaAssignment>().Entities
            .Include(a => a.User)
            .Include(a => a.Area)
            .Include(a => a.AssignedByUser);
    }

    private async Task<StaffAreaAssignmentDto> GetAssignmentDtoAsync(int assignmentId)
    {
        var assignment = await GetAssignmentQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.StaffAreaAssignmentId == assignmentId)
            ?? throw new Exception("Assignment khong ton tai.");

        return Map(assignment);
    }

    private async Task EnsureStaffExistsAsync(Guid userId)
    {
        var exists = await _uow.GetRepository<User>().Entities
            .AsNoTracking()
            .Include(u => u.Role)
            .AnyAsync(u =>
                u.UserId == userId &&
                u.IsActive &&
                u.Role.RoleName == UserRole.SYSTEMSTAFF);

        if (!exists)
        {
            throw new Exception("Staff khong ton tai hoac khong co role SYSTEMSTAFF.");
        }
    }

    private async Task EnsureAreaExistsAsync(int areaId)
    {
        var exists = await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .AnyAsync(a => a.AreaId == areaId && a.IsActive);

        if (!exists)
        {
            throw new Exception("Area khong ton tai hoac da bi khoa.");
        }
    }

    private static StaffAreaAssignmentDto Map(StaffAreaAssignment assignment)
    {
        return new StaffAreaAssignmentDto
        {
            StaffAreaAssignmentId = assignment.StaffAreaAssignmentId,
            UserId = assignment.UserId,
            StaffName = assignment.User?.FullName,
            AreaId = assignment.AreaId,
            AreaName = assignment.Area?.AreaName,
            AssignedByUserId = assignment.AssignedByUserId,
            AssignedByUserName = assignment.AssignedByUser?.FullName,
            IsPrimary = assignment.IsPrimary,
            StartDate = assignment.StartDate,
            EndDate = assignment.EndDate,
            IsActive = assignment.IsActive,
            CreatedAt = assignment.CreatedAt
        };
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
