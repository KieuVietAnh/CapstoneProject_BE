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
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public CategoriesController(IUnitOfWork uow)
    {
        _uow = uow;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeInactive = false)
    {
        var query = _uow.GetRepository<UrbanServiceCategory>().Entities.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        return Ok(categories.Select(Map).ToList());
    }

    [HttpGet("{categoryId:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCategory(int categoryId)
    {
        var category = await _uow.GetRepository<UrbanServiceCategory>().Entities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId)
            ?? throw new Exception("Category khong ton tai.");

        return Ok(Map(category));
    }

    [HttpPost]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            throw new Exception("CategoryName la bat buoc.");
        }

        var category = new UrbanServiceCategory
        {
            CategoryName = request.CategoryName.Trim(),
            Description = NormalizeOptional(request.Description),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<UrbanServiceCategory>().AddAsync(category);
        await _uow.SaveAsync();

        return Ok(Map(category));
    }

    [HttpPut("{categoryId:int}")]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCategory(
        int categoryId,
        [FromBody] CategoryUpdateRequest request)
    {
        var category = await _uow.GetRepository<UrbanServiceCategory>().GetByIdAsync(categoryId)
            ?? throw new Exception("Category khong ton tai.");

        if (!string.IsNullOrWhiteSpace(request.CategoryName))
        {
            category.CategoryName = request.CategoryName.Trim();
        }

        if (request.Description != null)
        {
            category.Description = NormalizeOptional(request.Description);
        }

        await _uow.SaveAsync();

        return Ok(Map(category));
    }

    [HttpPatch("{categoryId:int}/active")]
    [Authorize(Roles = UserRole.SYSTEMADMIN)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        int categoryId,
        [FromBody] SetActiveRequest request)
    {
        var category = await _uow.GetRepository<UrbanServiceCategory>().GetByIdAsync(categoryId)
            ?? throw new Exception("Category khong ton tai.");

        category.IsActive = request.IsActive;
        await _uow.SaveAsync();

        return Ok(Map(category));
    }

    private static CategoryDto Map(UrbanServiceCategory category)
    {
        return new CategoryDto
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
