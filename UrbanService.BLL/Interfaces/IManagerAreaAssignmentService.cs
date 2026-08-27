using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces;

public interface IManagerAreaAssignmentService
{
    Task<IReadOnlyCollection<ManagerAreaAssignmentDto>> GetAssignmentsAsync(
        ManagerAreaAssignmentQueryParameters query,
        CancellationToken cancellationToken = default);

    Task<ManagerAreaAssignmentDto> GetAssignmentAsync(
        int assignmentId,
        CancellationToken cancellationToken = default);

    Task<ManagerAreaAssignmentDto> CreateAsync(
        Guid adminUserId,
        ManagerAreaAssignmentCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ManagerAreaAssignmentDto> UpdateAsync(
        Guid adminUserId,
        int assignmentId,
        ManagerAreaAssignmentUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<ManagerAreaAssignmentDto> SetActiveAsync(
        Guid adminUserId,
        int assignmentId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
