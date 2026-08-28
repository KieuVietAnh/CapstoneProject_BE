using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class ManagerAreaAssignmentService : IManagerAreaAssignmentService
{
    private static readonly Expression<Func<ManagerAreaAssignment, ManagerAreaAssignmentDto>>
        AssignmentProjection = assignment => new ManagerAreaAssignmentDto
        {
            ManagerAreaAssignmentId = assignment.ManagerAreaAssignmentId,
            ManagerUserId = assignment.ManagerUserId,
            ManagerName = assignment.ManagerUser.FullName,
            ManagerEmail = assignment.ManagerUser.Email,
            ManagerIsActive = assignment.ManagerUser.IsActive,
            AreaId = assignment.AreaId,
            AreaName = assignment.Area.AreaName,
            WardCode = assignment.Area.WardCode,
            AreaIsActive = assignment.Area.IsActive,
            CreatedByUserId = assignment.CreatedByUserId,
            CreatedByUserName = assignment.CreatedByUser.FullName,
            UpdatedByUserId = assignment.UpdatedByUserId,
            UpdatedByUserName = assignment.UpdatedByUser != null
                ? assignment.UpdatedByUser.FullName
                : null,
            IsActive = assignment.IsActive,
            CreatedAt = assignment.CreatedAt,
            UpdatedAt = assignment.UpdatedAt
        };

    private readonly IUnitOfWork _uow;

    public ManagerAreaAssignmentService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyCollection<ManagerAreaAssignmentDto>> GetAssignmentsAsync(
        ManagerAreaAssignmentQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var assignments = _uow
            .GetRepository<ManagerAreaAssignment>()
            .Entities
            .AsNoTracking();

        if (query.ManagerUserId.HasValue)
        {
            assignments = assignments.Where(
                assignment => assignment.ManagerUserId == query.ManagerUserId.Value);
        }

        if (query.AreaId.HasValue)
        {
            assignments = assignments.Where(
                assignment => assignment.AreaId == query.AreaId.Value);
        }

        if (query.IsActive.HasValue)
        {
            assignments = assignments.Where(
                assignment => assignment.IsActive == query.IsActive.Value);
        }

        return await assignments
            .OrderByDescending(assignment => assignment.IsActive)
            .ThenBy(assignment => assignment.Area.AreaName)
            .ThenBy(assignment => assignment.ManagerUser.FullName)
            .Select(AssignmentProjection)
            .ToListAsync(cancellationToken);
    }

    public async Task<ManagerAreaAssignmentDto> GetAssignmentAsync(
        int assignmentId,
        CancellationToken cancellationToken = default)
    {
        return await GetAssignmentQuery(assignmentId)
            .Select(AssignmentProjection)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Phạm vi quản lý của Manager không tồn tại.");
    }

    public async Task<ManagerAreaAssignmentDto> CreateAsync(
        Guid adminUserId,
        ManagerAreaAssignmentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminExistsAsync(adminUserId, cancellationToken);
        ValidateRequest(request.ManagerUserId, request.AreaId);
        await EnsureManagerExistsAsync(request.ManagerUserId, cancellationToken);
        await EnsureAreaExistsAsync(request.AreaId, cancellationToken);

        var repository = _uow.GetRepository<ManagerAreaAssignment>();
        var existing = await repository.Entities.FirstOrDefaultAsync(
            assignment =>
                assignment.ManagerUserId == request.ManagerUserId &&
                assignment.AreaId == request.AreaId,
            cancellationToken);
        var now = DateTime.UtcNow;

        if (existing != null)
        {
            existing.IsActive = true;
            existing.UpdatedByUserId = adminUserId;
            existing.UpdatedAt = now;
            await _uow.SaveAsync();
            return await GetAssignmentAsync(existing.ManagerAreaAssignmentId, cancellationToken);
        }

        var assignment = new ManagerAreaAssignment
        {
            ManagerUserId = request.ManagerUserId,
            AreaId = request.AreaId,
            CreatedByUserId = adminUserId,
            IsActive = true,
            CreatedAt = now
        };

        await repository.AddAsync(assignment);
        await _uow.SaveAsync();

        return await GetAssignmentAsync(assignment.ManagerAreaAssignmentId, cancellationToken);
    }

    public async Task<ManagerAreaAssignmentDto> UpdateAsync(
        Guid adminUserId,
        int assignmentId,
        ManagerAreaAssignmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminExistsAsync(adminUserId, cancellationToken);
        if (request.AreaId <= 0)
        {
            throw new Exception("AreaId không hợp lệ.");
        }

        var repository = _uow.GetRepository<ManagerAreaAssignment>();
        var assignment = await repository.Entities.FirstOrDefaultAsync(
            item => item.ManagerAreaAssignmentId == assignmentId,
            cancellationToken)
            ?? throw new Exception("Phạm vi quản lý của Manager không tồn tại.");

        await EnsureManagerExistsAsync(assignment.ManagerUserId, cancellationToken);
        await EnsureAreaExistsAsync(request.AreaId, cancellationToken);

        var duplicate = await repository.Entities.AsNoTracking().AnyAsync(
            item =>
                item.ManagerAreaAssignmentId != assignmentId &&
                item.ManagerUserId == assignment.ManagerUserId &&
                item.AreaId == request.AreaId,
            cancellationToken);
        if (duplicate)
        {
            throw new Exception("Manager đã được phân quyền quản lý phường này.");
        }

        assignment.AreaId = request.AreaId;
        assignment.UpdatedByUserId = adminUserId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return await GetAssignmentAsync(assignmentId, cancellationToken);
    }

    public async Task<ManagerAreaAssignmentDto> SetActiveAsync(
        Guid adminUserId,
        int assignmentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminExistsAsync(adminUserId, cancellationToken);
        var assignment = await _uow
            .GetRepository<ManagerAreaAssignment>()
            .Entities
            .FirstOrDefaultAsync(
                item => item.ManagerAreaAssignmentId == assignmentId,
                cancellationToken)
            ?? throw new Exception("Phạm vi quản lý của Manager không tồn tại.");

        if (isActive)
        {
            await EnsureManagerExistsAsync(assignment.ManagerUserId, cancellationToken);
            await EnsureAreaExistsAsync(assignment.AreaId, cancellationToken);
        }

        assignment.IsActive = isActive;
        assignment.UpdatedByUserId = adminUserId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return await GetAssignmentAsync(assignmentId, cancellationToken);
    }

    private IQueryable<ManagerAreaAssignment> GetAssignmentQuery(int assignmentId)
    {
        return _uow
            .GetRepository<ManagerAreaAssignment>()
            .Entities
            .AsNoTracking()
            .Where(assignment => assignment.ManagerAreaAssignmentId == assignmentId);
    }

    private async Task EnsureManagerExistsAsync(
        Guid managerUserId,
        CancellationToken cancellationToken)
    {
        var exists = await _uow
            .GetRepository<User>()
            .Entities
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.UserId == managerUserId &&
                    user.IsActive &&
                    user.Role.RoleName.ToUpper() == UserRole.INTERACTIONMANAGER,
                cancellationToken);

        if (!exists)
        {
            throw new Exception("Manager không tồn tại, đã bị khóa hoặc không có role INTERACTIONMANAGER.");
        }
    }

    private async Task EnsureAreaExistsAsync(
        int areaId,
        CancellationToken cancellationToken)
    {
        var exists = await _uow
            .GetRepository<OperatingArea>()
            .Entities
            .AsNoTracking()
            .AnyAsync(
                area => area.AreaId == areaId && area.IsActive,
                cancellationToken);

        if (!exists)
        {
            throw new Exception("Phường không tồn tại hoặc đã bị khóa.");
        }
    }

    private async Task EnsureAdminExistsAsync(
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        if (adminUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException();
        }

        var isAdmin = await _uow
            .GetRepository<User>()
            .Entities
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.UserId == adminUserId &&
                    user.IsActive &&
                    user.Role.RoleName.ToUpper() == UserRole.SYSTEMADMIN,
                cancellationToken);
        if (!isAdmin)
        {
            throw new ForbiddenAccessException(
                "Chỉ System Admin đang hoạt động được quản lý phạm vi của Manager.");
        }
    }

    private static void ValidateRequest(Guid managerUserId, int areaId)
    {
        if (managerUserId == Guid.Empty)
        {
            throw new Exception("ManagerUserId không hợp lệ.");
        }

        if (areaId <= 0)
        {
            throw new Exception("AreaId không hợp lệ.");
        }
    }
}
