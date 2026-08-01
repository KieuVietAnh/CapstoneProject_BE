using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs.SLA;

namespace UrbanService.BLL.Interfaces;

public interface ISlaPolicyService
{
    Task<SlaPolicyDto> CreateAsync(
        Guid currentUserId,
        SlaPolicyCreateRequest request);

    Task<SlaPolicyDto> UpdateAsync(
        Guid currentUserId,
        int slaPolicyId,
        SlaPolicyUpdateRequest request);

    Task<SlaPolicyDto> GetByIdAsync(int slaPolicyId);

    Task<PagedResultDto<SlaPolicyDto>> GetAllAsync(
        SlaPolicyQueryParameters query);

    Task SetActiveAsync(
        Guid currentUserId,
        int slaPolicyId,
        bool isActive);

    Task DeleteAsync(int slaPolicyId);
}