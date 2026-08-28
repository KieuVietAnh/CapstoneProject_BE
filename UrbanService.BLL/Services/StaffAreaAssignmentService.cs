using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class StaffAreaAssignmentService : IStaffAreaAssignmentService
{
    private static readonly Expression<Func<StaffAreaAssignment, StaffAreaAssignmentDto>>
        AssignmentProjection = assignment => new StaffAreaAssignmentDto
        {
            StaffAreaAssignmentId = assignment.StaffAreaAssignmentId,
            UserId = assignment.UserId,
            StaffName = assignment.User.FullName,
            AreaId = assignment.AreaId,
            AreaName = assignment.Area.AreaName,
            CategoryId = assignment.CategoryId,
            CategoryName = assignment.Category != null
                ? assignment.Category.CategoryName
                : null,
            AssignedByUserId = assignment.AssignedByUserId,
            AssignedByUserName = assignment.AssignedByUser != null
                ? assignment.AssignedByUser.FullName
                : null,
            IsPrimary = assignment.IsPrimary,
            StartDate = assignment.StartDate,
            EndDate = assignment.EndDate,
            IsActive = assignment.IsActive,
            CreatedAt = assignment.CreatedAt
        };

    private readonly IUnitOfWork _uow;

    public StaffAreaAssignmentService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyCollection<StaffAreaAssignmentDto>> GetAssignmentsAsync(
        Guid actorUserId,
        Guid? userId = null,
        int? areaId = null,
        int? categoryId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetAuthorizedActorAsync(actorUserId, cancellationToken);
        var assignments = _uow.GetRepository<StaffAreaAssignment>().Entities.AsNoTracking();

        if (actor.RoleName == UserRole.INTERACTIONMANAGER)
        {
            assignments = assignments.Where(
                assignment => actor.ManagerAreaIds.Contains(assignment.AreaId));
        }

        if (userId.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.UserId == userId.Value);
        }

        if (areaId.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.AreaId == areaId.Value);
        }

        if (categoryId.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.CategoryId == categoryId.Value);
        }

        if (isActive.HasValue)
        {
            assignments = assignments.Where(assignment => assignment.IsActive == isActive.Value);
        }

        return await assignments
            .OrderByDescending(assignment => assignment.IsActive)
            .ThenBy(assignment => assignment.Area.AreaName)
            .ThenBy(assignment => assignment.User.FullName)
            .Select(AssignmentProjection)
            .ToListAsync(cancellationToken);
    }

    public async Task<StaffAreaAssignmentDto> CreateAsync(
        Guid actorUserId,
        StaffAreaAssignmentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetAuthorizedActorAsync(actorUserId, cancellationToken);
        EnsureAreaAccess(actor, request.AreaId);
        await EnsureStaffExistsAsync(request.UserId, cancellationToken);
        await EnsureAreaExistsAsync(request.AreaId, cancellationToken);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        var repository = _uow.GetRepository<StaffAreaAssignment>();
        var existing = await repository.Entities.FirstOrDefaultAsync(
            assignment =>
                assignment.UserId == request.UserId &&
                assignment.AreaId == request.AreaId &&
                assignment.CategoryId == request.CategoryId,
            cancellationToken);

        if (existing != null)
        {
            if (actor.RoleName == UserRole.INTERACTIONMANAGER && !existing.IsActive)
            {
                throw new ForbiddenAccessException(
                    "Manager chỉ được vô hiệu hóa, không được kích hoạt lại phân công Staff.");
            }

            existing.IsActive = true;
            existing.IsPrimary = request.IsPrimary;
            existing.StartDate = request.StartDate;
            existing.EndDate = request.EndDate;
            await _uow.SaveAsync();

            return await GetAssignmentDtoAsync(
                existing.StaffAreaAssignmentId,
                cancellationToken);
        }

        var assignment = new StaffAreaAssignment
        {
            UserId = request.UserId,
            AreaId = request.AreaId,
            CategoryId = request.CategoryId,
            AssignedByUserId = actor.UserId,
            IsPrimary = request.IsPrimary,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(assignment);
        await _uow.SaveAsync();

        return await GetAssignmentDtoAsync(
            assignment.StaffAreaAssignmentId,
            cancellationToken);
    }

    public async Task<StaffAreaAssignmentDto> UpdateAsync(
        Guid actorUserId,
        int assignmentId,
        StaffAreaAssignmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetAuthorizedActorAsync(actorUserId, cancellationToken);
        var repository = _uow.GetRepository<StaffAreaAssignment>();
        var assignment = await repository.Entities.FirstOrDefaultAsync(
            item => item.StaffAreaAssignmentId == assignmentId,
            cancellationToken)
            ?? throw new Exception("Assignment không tồn tại.");

        EnsureAreaAccess(actor, assignment.AreaId);
        EnsureAreaAccess(actor, request.AreaId);
        await EnsureAreaExistsAsync(request.AreaId, cancellationToken);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        var duplicate = await repository.Entities.AsNoTracking().AnyAsync(
            item =>
                item.StaffAreaAssignmentId != assignmentId &&
                item.UserId == assignment.UserId &&
                item.AreaId == request.AreaId &&
                item.CategoryId == request.CategoryId,
            cancellationToken);

        if (duplicate)
        {
            throw new Exception("Staff đã có phạm vi phụ trách khu vực và danh mục này.");
        }

        assignment.AreaId = request.AreaId;
        assignment.CategoryId = request.CategoryId;
        assignment.IsPrimary = request.IsPrimary;
        assignment.StartDate = request.StartDate;
        assignment.EndDate = request.EndDate;
        await _uow.SaveAsync();

        return await GetAssignmentDtoAsync(assignmentId, cancellationToken);
    }

    public async Task<StaffAreaAssignmentDto> SetActiveAsync(
        Guid actorUserId,
        int assignmentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetAuthorizedActorAsync(actorUserId, cancellationToken);
        var assignment = await _uow.GetRepository<StaffAreaAssignment>().Entities
            .FirstOrDefaultAsync(
                item => item.StaffAreaAssignmentId == assignmentId,
                cancellationToken)
            ?? throw new Exception("Assignment không tồn tại.");

        EnsureAreaAccess(actor, assignment.AreaId);
        if (actor.RoleName == UserRole.INTERACTIONMANAGER && isActive)
        {
            throw new ForbiddenAccessException(
                "Manager chỉ được vô hiệu hóa, không được kích hoạt lại phân công Staff.");
        }

        assignment.IsActive = isActive;
        await _uow.SaveAsync();

        return await GetAssignmentDtoAsync(assignmentId, cancellationToken);
    }

    private async Task<ManagementActorScope> GetAuthorizedActorAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var actor = await ManagementAccessRules.GetActorScopeAsync(
            _uow,
            actorUserId,
            cancellationToken);

        if (actor.RoleName != UserRole.SYSTEMADMIN &&
            actor.RoleName != UserRole.INTERACTIONMANAGER)
        {
            throw new ForbiddenAccessException(
                "Chỉ System Admin hoặc Interaction Manager được quản lý phân công Staff.");
        }

        return actor;
    }

    private static void EnsureAreaAccess(ManagementActorScope actor, int areaId)
    {
        if (actor.RoleName == UserRole.INTERACTIONMANAGER &&
            !actor.ManagerAreaIds.Contains(areaId))
        {
            throw new ForbiddenAccessException(
                "Manager không phụ trách khu vực của phân công này.");
        }
    }

    private async Task<StaffAreaAssignmentDto> GetAssignmentDtoAsync(
        int assignmentId,
        CancellationToken cancellationToken)
    {
        return await _uow.GetRepository<StaffAreaAssignment>().Entities
            .AsNoTracking()
            .Where(assignment => assignment.StaffAreaAssignmentId == assignmentId)
            .Select(AssignmentProjection)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Exception("Assignment không tồn tại.");
    }

    private async Task EnsureStaffExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exists = await _uow.GetRepository<User>().Entities
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.UserId == userId &&
                    user.IsActive &&
                    user.Role.RoleName.ToUpper() == UserRole.SYSTEMSTAFF,
                cancellationToken);

        if (!exists)
        {
            throw new Exception("Staff không tồn tại hoặc không có role SYSTEMSTAFF.");
        }
    }

    private async Task EnsureAreaExistsAsync(
        int areaId,
        CancellationToken cancellationToken)
    {
        var exists = await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .AnyAsync(
                area => area.AreaId == areaId && area.IsActive,
                cancellationToken);

        if (!exists)
        {
            throw new Exception("Khu vực không tồn tại hoặc đã bị khóa.");
        }
    }

    private async Task EnsureCategoryExistsAsync(
        int? categoryId,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await _uow.GetRepository<UrbanServiceCategory>().Entities
            .AsNoTracking()
            .AnyAsync(
                category => category.CategoryId == categoryId.Value && category.IsActive,
                cancellationToken);

        if (!exists)
        {
            throw new Exception("Danh mục không tồn tại hoặc đã bị khóa.");
        }
    }
}
