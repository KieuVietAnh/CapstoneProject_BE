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
[Authorize]
[Route("api/areas")]
public class AreasController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public AreasController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<AreaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAreas(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null)
    {
        var query = _uow.GetRepository<OperatingArea>().Entities.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLower();
            query = query.Where(a =>
                a.AreaName.ToLower().Contains(normalized) ||
                (a.DistrictName != null && a.DistrictName.ToLower().Contains(normalized)) ||
                (a.ProvinceName != null && a.ProvinceName.ToLower().Contains(normalized)) ||
                (a.WardCode != null && a.WardCode.ToLower().Contains(normalized)));
        }

        var areas = await query
            .OrderBy(a => a.ProvinceName)
            .ThenBy(a => a.DistrictName)
            .ThenBy(a => a.AreaName)
            .ToListAsync();

        return Ok(areas.Select(Map).ToList());
    }

    [HttpGet("{areaId:int}")]
    [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetArea(int areaId)
    {
        var area = await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AreaId == areaId)
            ?? throw new Exception("Area khong ton tai.");

        return Ok(Map(area));
    }

    [HttpPost]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateArea([FromBody] AreaCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AreaName))
        {
            throw new Exception("AreaName la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(request.AreaType))
        {
            throw new Exception("AreaType la bat buoc.");
        }

        var area = new OperatingArea
        {
            AreaName = request.AreaName.Trim(),
            AreaType = request.AreaType.Trim(),
            WardCode = NormalizeOptional(request.WardCode),
            DistrictName = NormalizeOptional(request.DistrictName),
            ProvinceName = NormalizeOptional(request.ProvinceName),
            CenterLatitude = request.CenterLatitude,
            CenterLongitude = request.CenterLongitude,
            BoundaryGeoJson = NormalizeOptional(request.BoundaryGeoJson),
            IsActive = true,
            StartedAt = request.StartedAt,
            EndedAt = request.EndedAt,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<OperatingArea>().AddAsync(area);
        await _uow.SaveAsync();

        return Ok(Map(area));
    }

    [HttpPut("{areaId:int}")]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateArea(
        int areaId,
        [FromBody] AreaUpdateRequest request)
    {
        var area = await _uow.GetRepository<OperatingArea>().GetByIdAsync(areaId)
            ?? throw new Exception("Area khong ton tai.");

        if (!string.IsNullOrWhiteSpace(request.AreaName))
        {
            area.AreaName = request.AreaName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.AreaType))
        {
            area.AreaType = request.AreaType.Trim();
        }

        if (request.WardCode != null)
        {
            area.WardCode = NormalizeOptional(request.WardCode);
        }

        if (request.DistrictName != null)
        {
            area.DistrictName = NormalizeOptional(request.DistrictName);
        }

        if (request.ProvinceName != null)
        {
            area.ProvinceName = NormalizeOptional(request.ProvinceName);
        }

        area.CenterLatitude = request.CenterLatitude ?? area.CenterLatitude;
        area.CenterLongitude = request.CenterLongitude ?? area.CenterLongitude;

        if (request.BoundaryGeoJson != null)
        {
            area.BoundaryGeoJson = NormalizeOptional(request.BoundaryGeoJson);
        }

        area.StartedAt = request.StartedAt ?? area.StartedAt;
        area.EndedAt = request.EndedAt ?? area.EndedAt;
        area.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveAsync();

        return Ok(Map(area));
    }

    [HttpPatch("{areaId:int}/active")]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        int areaId,
        [FromBody] SetActiveRequest request)
    {
        var area = await _uow.GetRepository<OperatingArea>().GetByIdAsync(areaId)
            ?? throw new Exception("Area khong ton tai.");

        area.IsActive = request.IsActive;
        area.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return Ok(Map(area));
    }

    private static AreaDto Map(OperatingArea area)
    {
        return new AreaDto
        {
            AreaId = area.AreaId,
            AreaName = area.AreaName,
            AreaType = area.AreaType,
            WardCode = area.WardCode,
            DistrictName = area.DistrictName,
            ProvinceName = area.ProvinceName,
            CenterLatitude = area.CenterLatitude,
            CenterLongitude = area.CenterLongitude,
            BoundaryGeoJson = area.BoundaryGeoJson,
            IsActive = area.IsActive,
            StartedAt = area.StartedAt,
            EndedAt = area.EndedAt,
            CreatedAt = area.CreatedAt,
            UpdatedAt = area.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
