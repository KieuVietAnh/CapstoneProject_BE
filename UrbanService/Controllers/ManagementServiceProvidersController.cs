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
[Route("api/management/service-providers")]
public class ManagementServiceProvidersController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public ManagementServiceProvidersController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ServiceProviderCoordinatorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceProviders(
        [FromQuery] bool includeInactive = false,
        [FromQuery] int? areaId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null)
    {
        var query = _uow.GetRepository<ServiceProviderCoordinator>().Entities
            .AsNoTracking()
            .Include(c => c.CoordinatorCoverages)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (areaId.HasValue)
        {
            query = query.Where(c => c.CoordinatorCoverages.Any(coverage =>
                coverage.AreaId == areaId.Value && coverage.IsActive));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(c => c.CoordinatorCoverages.Any(coverage =>
                coverage.CategoryId == categoryId.Value && coverage.IsActive));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLower();
            query = query.Where(c =>
                c.ProviderName.ToLower().Contains(normalized) ||
                c.CoordinatorName.ToLower().Contains(normalized) ||
                c.PhoneNumber.ToLower().Contains(normalized) ||
                (c.Email != null && c.Email.ToLower().Contains(normalized)));
        }

        var providers = await query
            .OrderBy(c => c.ProviderName)
            .ThenBy(c => c.CoordinatorName)
            .ToListAsync();

        return Ok(providers.Select(MapProvider).ToList());
    }

    [HttpGet("{coordinatorId:int}")]
    [ProducesResponseType(typeof(ServiceProviderCoordinatorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetServiceProvider(int coordinatorId)
    {
        var provider = await _uow.GetRepository<ServiceProviderCoordinator>().Entities
            .AsNoTracking()
            .Include(c => c.CoordinatorCoverages)
            .FirstOrDefaultAsync(c => c.CoordinatorId == coordinatorId)
            ?? throw new Exception("Service Provider khong ton tai.");

        return Ok(MapProvider(provider));
    }

    [HttpPost]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(ServiceProviderCoordinatorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateServiceProvider(
        [FromBody] ServiceProviderCoordinatorCreateRequest request)
    {
        ValidateProvider(request.ProviderName, request.CoordinatorName, request.PhoneNumber);

        var provider = new ServiceProviderCoordinator
        {
            ProviderName = request.ProviderName.Trim(),
            CoordinatorName = request.CoordinatorName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Email = NormalizeOptional(request.Email),
            Address = NormalizeOptional(request.Address),
            Note = NormalizeOptional(request.Note),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<ServiceProviderCoordinator>().AddAsync(provider);
        await _uow.SaveAsync();

        return Ok(MapProvider(provider));
    }

    [HttpPut("{coordinatorId:int}")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(ServiceProviderCoordinatorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateServiceProvider(
        int coordinatorId,
        [FromBody] ServiceProviderCoordinatorUpdateRequest request)
    {
        var provider = await _uow.GetRepository<ServiceProviderCoordinator>().Entities
            .Include(c => c.CoordinatorCoverages)
            .FirstOrDefaultAsync(c => c.CoordinatorId == coordinatorId)
            ?? throw new Exception("Service Provider khong ton tai.");

        if (!string.IsNullOrWhiteSpace(request.ProviderName))
        {
            provider.ProviderName = request.ProviderName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.CoordinatorName))
        {
            provider.CoordinatorName = request.CoordinatorName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            provider.PhoneNumber = request.PhoneNumber.Trim();
        }

        if (request.Email != null)
        {
            provider.Email = NormalizeOptional(request.Email);
        }

        if (request.Address != null)
        {
            provider.Address = NormalizeOptional(request.Address);
        }

        if (request.Note != null)
        {
            provider.Note = NormalizeOptional(request.Note);
        }

        provider.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return Ok(MapProvider(provider));
    }

    [HttpPatch("{coordinatorId:int}/active")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(ServiceProviderCoordinatorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        int coordinatorId,
        [FromBody] SetActiveRequest request)
    {
        var provider = await _uow.GetRepository<ServiceProviderCoordinator>().Entities
            .Include(c => c.CoordinatorCoverages)
            .FirstOrDefaultAsync(c => c.CoordinatorId == coordinatorId)
            ?? throw new Exception("Service Provider khong ton tai.");

        provider.IsActive = request.IsActive;
        provider.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return Ok(MapProvider(provider));
    }

    [HttpGet("{coordinatorId:int}/coverages")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CoordinatorCoverageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCoverages(int coordinatorId)
    {
        await EnsureCoordinatorExistsAsync(coordinatorId);

        var coverages = await _uow.GetRepository<CoordinatorCoverage>().Entities
            .AsNoTracking()
            .Include(c => c.Area)
            .Include(c => c.Category)
            .Where(c => c.CoordinatorId == coordinatorId)
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.IsPrimary)
            .ThenBy(c => c.PriorityOrder)
            .ToListAsync();

        return Ok(coverages.Select(MapCoverage).ToList());
    }

    [HttpPost("{coordinatorId:int}/coverages")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(CoordinatorCoverageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCoverage(
        int coordinatorId,
        [FromBody] CoordinatorCoverageCreateRequest request)
    {
        await EnsureCoordinatorExistsAsync(coordinatorId);
        await EnsureAreaExistsAsync(request.AreaId);
        await EnsureCategoryExistsAsync(request.CategoryId);

        var exists = await _uow.GetRepository<CoordinatorCoverage>().Entities
            .AsNoTracking()
            .AnyAsync(c =>
                c.CoordinatorId == coordinatorId &&
                c.AreaId == request.AreaId &&
                c.CategoryId == request.CategoryId);

        if (exists)
        {
            throw new Exception("Coverage cho provider/area/category nay da ton tai.");
        }

        var coverage = new CoordinatorCoverage
        {
            CoordinatorId = coordinatorId,
            AreaId = request.AreaId,
            CategoryId = request.CategoryId,
            IsPrimary = request.IsPrimary,
            PriorityOrder = request.PriorityOrder < 1 ? 1 : request.PriorityOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<CoordinatorCoverage>().AddAsync(coverage);
        await _uow.SaveAsync();

        return Ok(await GetCoverageDtoAsync(coverage.CoverageId));
    }

    [HttpPut("{coordinatorId:int}/coverages/{coverageId:int}")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.INTERACTIONMANAGER)]
    [ProducesResponseType(typeof(CoordinatorCoverageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCoverage(
        int coordinatorId,
        int coverageId,
        [FromBody] CoordinatorCoverageUpdateRequest request)
    {
        var coverage = await _uow.GetRepository<CoordinatorCoverage>().Entities
            .FirstOrDefaultAsync(c => c.CoverageId == coverageId && c.CoordinatorId == coordinatorId)
            ?? throw new Exception("Coverage khong ton tai.");

        if (request.AreaId.HasValue)
        {
            await EnsureAreaExistsAsync(request.AreaId.Value);
            coverage.AreaId = request.AreaId.Value;
        }

        if (request.CategoryId.HasValue)
        {
            await EnsureCategoryExistsAsync(request.CategoryId.Value);
            coverage.CategoryId = request.CategoryId.Value;
        }

        var duplicateCoverageExists = await _uow.GetRepository<CoordinatorCoverage>().Entities
            .AsNoTracking()
            .AnyAsync(c =>
                c.CoverageId != coverageId &&
                c.CoordinatorId == coordinatorId &&
                c.AreaId == coverage.AreaId &&
                c.CategoryId == coverage.CategoryId);

        if (duplicateCoverageExists)
        {
            throw new Exception("Coverage cho provider/area/category nay da ton tai.");
        }

        coverage.IsPrimary = request.IsPrimary ?? coverage.IsPrimary;
        coverage.PriorityOrder = request.PriorityOrder.HasValue
            ? Math.Max(1, request.PriorityOrder.Value)
            : coverage.PriorityOrder;
        coverage.IsActive = request.IsActive ?? coverage.IsActive;

        await _uow.SaveAsync();

        return Ok(await GetCoverageDtoAsync(coverageId));
    }

    private async Task<CoordinatorCoverageDto> GetCoverageDtoAsync(int coverageId)
    {
        var coverage = await _uow.GetRepository<CoordinatorCoverage>().Entities
            .AsNoTracking()
            .Include(c => c.Area)
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.CoverageId == coverageId)
            ?? throw new Exception("Coverage khong ton tai.");

        return MapCoverage(coverage);
    }

    private async Task EnsureCoordinatorExistsAsync(int coordinatorId)
    {
        var exists = await _uow.GetRepository<ServiceProviderCoordinator>().Entities
            .AsNoTracking()
            .AnyAsync(c => c.CoordinatorId == coordinatorId);

        if (!exists)
        {
            throw new Exception("Service Provider khong ton tai.");
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

    private async Task EnsureCategoryExistsAsync(int categoryId)
    {
        var exists = await _uow.GetRepository<UrbanServiceCategory>().Entities
            .AsNoTracking()
            .AnyAsync(c => c.CategoryId == categoryId && c.IsActive);

        if (!exists)
        {
            throw new Exception("Category khong ton tai hoac da bi khoa.");
        }
    }

    private static ServiceProviderCoordinatorDto MapProvider(ServiceProviderCoordinator provider)
    {
        return new ServiceProviderCoordinatorDto
        {
            CoordinatorId = provider.CoordinatorId,
            ProviderName = provider.ProviderName,
            CoordinatorName = provider.CoordinatorName,
            PhoneNumber = provider.PhoneNumber,
            Email = provider.Email,
            Address = provider.Address,
            Note = provider.Note,
            IsActive = provider.IsActive,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt,
            CoverageCount = provider.CoordinatorCoverages.Count
        };
    }

    private static CoordinatorCoverageDto MapCoverage(CoordinatorCoverage coverage)
    {
        return new CoordinatorCoverageDto
        {
            CoverageId = coverage.CoverageId,
            CoordinatorId = coverage.CoordinatorId,
            AreaId = coverage.AreaId,
            AreaName = coverage.Area?.AreaName,
            CategoryId = coverage.CategoryId,
            CategoryName = coverage.Category?.CategoryName,
            IsPrimary = coverage.IsPrimary,
            PriorityOrder = coverage.PriorityOrder,
            IsActive = coverage.IsActive,
            CreatedAt = coverage.CreatedAt
        };
    }

    private static void ValidateProvider(string providerName, string coordinatorName, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new Exception("ProviderName la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(coordinatorName))
        {
            throw new Exception("CoordinatorName la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new Exception("PhoneNumber la bat buoc.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
