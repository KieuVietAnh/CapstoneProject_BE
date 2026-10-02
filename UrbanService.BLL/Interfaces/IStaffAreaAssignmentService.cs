using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IStaffAreaAssignmentService
{
    Task<IReadOnlyCollection<StaffAreaAssignmentDto>> GetAssignmentsAsync(
        Guid actorUserId,
        Guid? userId = null,
        int? areaId = null,
        int? categoryId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AreaDto>> GetManagedAreasAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<ManagedStaffAccountDto> CreateStaffAccountAsync(
        Guid actorUserId,
        ManagedStaffAccountCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffAreaAssignmentDto> CreateAsync(
        Guid actorUserId,
        StaffAreaAssignmentCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffAreaAssignmentDto> UpdateAsync(
        Guid actorUserId,
        int assignmentId,
        StaffAreaAssignmentUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffAreaAssignmentDto> SetActiveAsync(
        Guid actorUserId,
        int assignmentId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
