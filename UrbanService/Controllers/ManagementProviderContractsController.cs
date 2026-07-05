using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.Controllers;

[ApiController]
[Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF + "," + UserRole.INTERACTIONMANAGER)]
[Route("api/management/provider-contracts")]
public class ManagementProviderContractsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ICloudinaryService _cloudinaryService;

    public ManagementProviderContractsController(
        IUnitOfWork uow,
        ICloudinaryService cloudinaryService)
    {
        _uow = uow;
        _cloudinaryService = cloudinaryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderContractDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContracts(
        [FromQuery] int? coordinatorId = null,
        [FromQuery] int? areaId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? status = null)
    {
        var query = _uow.GetRepository<ProviderContract>().Entities
            .AsNoTracking()
            .Include(c => c.Coordinator)
            .Include(c => c.Area)
            .Include(c => c.Category)
            .Include(c => c.CreatedByUser)
            .Include(c => c.ProviderContractAttachments)
            .AsQueryable();

        if (coordinatorId.HasValue)
        {
            query = query.Where(c => c.CoordinatorId == coordinatorId.Value);
        }

        if (areaId.HasValue)
        {
            query = query.Where(c => c.AreaId == areaId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLower();
            query = query.Where(c => c.Status.ToLower() == normalized);
        }

        var contracts = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Ok(contracts.Select(MapContract).ToList());
    }

    [HttpGet("{contractId:int}")]
    [ProducesResponseType(typeof(ProviderContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetContract(int contractId)
    {
        var contract = await GetContractQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ContractId == contractId)
            ?? throw new Exception("Contract khong ton tai.");

        return Ok(MapContract(contract));
    }

    [HttpPost]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(ProviderContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateContract([FromBody] ProviderContractCreateRequest request)
    {
        await EnsureCoordinatorExistsAsync(request.CoordinatorId);
        await EnsureOptionalAreaExistsAsync(request.AreaId);
        await EnsureOptionalCategoryExistsAsync(request.CategoryId);
        ValidateContract(request.ContractCode, request.ContractName, request.StartDate);

        var contract = new ProviderContract
        {
            CoordinatorId = request.CoordinatorId,
            AreaId = request.AreaId,
            CategoryId = request.CategoryId,
            ContractCode = request.ContractCode.Trim(),
            ContractName = request.ContractName.Trim(),
            Description = NormalizeOptional(request.Description),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim(),
            CreatedByUserId = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow
        };

        await _uow.GetRepository<ProviderContract>().AddAsync(contract);
        await _uow.SaveAsync();

        return Ok(await GetContractDtoAsync(contract.ContractId));
    }

    [HttpPut("{contractId:int}")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF)]
    [ProducesResponseType(typeof(ProviderContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateContract(
        int contractId,
        [FromBody] ProviderContractUpdateRequest request)
    {
        var contract = await _uow.GetRepository<ProviderContract>().GetByIdAsync(contractId)
            ?? throw new Exception("Contract khong ton tai.");

        if (request.CoordinatorId.HasValue)
        {
            await EnsureCoordinatorExistsAsync(request.CoordinatorId.Value);
            contract.CoordinatorId = request.CoordinatorId.Value;
        }

        if (request.AreaId.HasValue)
        {
            await EnsureOptionalAreaExistsAsync(request.AreaId);
            contract.AreaId = request.AreaId;
        }

        if (request.CategoryId.HasValue)
        {
            await EnsureOptionalCategoryExistsAsync(request.CategoryId);
            contract.CategoryId = request.CategoryId;
        }

        if (!string.IsNullOrWhiteSpace(request.ContractCode))
        {
            contract.ContractCode = request.ContractCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ContractName))
        {
            contract.ContractName = request.ContractName.Trim();
        }

        if (request.Description != null)
        {
            contract.Description = NormalizeOptional(request.Description);
        }

        contract.StartDate = request.StartDate ?? contract.StartDate;
        contract.EndDate = request.EndDate ?? contract.EndDate;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            contract.Status = request.Status.Trim();
        }

        contract.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveAsync();

        return Ok(await GetContractDtoAsync(contractId));
    }

    [HttpGet("{contractId:int}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderContractAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAttachments(int contractId)
    {
        await EnsureContractExistsAsync(contractId);

        var attachments = await _uow.GetRepository<ProviderContractAttachment>().Entities
            .AsNoTracking()
            .Include(a => a.UploadedByUser)
            .Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();

        return Ok(attachments.Select(MapAttachment).ToList());
    }

    [HttpPost("{contractId:int}/attachments")]
    [Authorize(Roles = UserRole.SYSTEMADMIN + "," + UserRole.SYSTEMSTAFF)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProviderContractAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAttachments(
        int contractId,
        [FromForm] ProviderContractAttachmentUploadRequest form)
    {
        await EnsureContractExistsAsync(contractId);

        if (form.Files == null || form.Files.Count == 0)
        {
            throw new Exception("Files la bat buoc.");
        }

        foreach (var file in form.Files.Where(f => f.Length > 0))
        {
            await using var stream = file.OpenReadStream();
            var uploadResult = await _cloudinaryService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                "urban-service/provider-contracts",
                HttpContext.RequestAborted);

            await _uow.GetRepository<ProviderContractAttachment>().AddAsync(
                new ProviderContractAttachment
                {
                    ContractId = contractId,
                    FileUrl = uploadResult.FileUrl,
                    FileType = uploadResult.FileType,
                    Description = NormalizeOptional(form.Description),
                    UploadedByUserId = GetCurrentUserId(),
                    UploadedAt = DateTime.UtcNow
                });
        }

        await _uow.SaveAsync();

        var attachments = await _uow.GetRepository<ProviderContractAttachment>().Entities
            .AsNoTracking()
            .Include(a => a.UploadedByUser)
            .Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();

        return Ok(attachments.Select(MapAttachment).ToList());
    }

    private IQueryable<ProviderContract> GetContractQuery()
    {
        return _uow.GetRepository<ProviderContract>().Entities
            .Include(c => c.Coordinator)
            .Include(c => c.Area)
            .Include(c => c.Category)
            .Include(c => c.CreatedByUser)
            .Include(c => c.ProviderContractAttachments);
    }

    private async Task<ProviderContractDto> GetContractDtoAsync(int contractId)
    {
        var contract = await GetContractQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ContractId == contractId)
            ?? throw new Exception("Contract khong ton tai.");

        return MapContract(contract);
    }

    private async Task EnsureContractExistsAsync(int contractId)
    {
        var exists = await _uow.GetRepository<ProviderContract>().Entities
            .AsNoTracking()
            .AnyAsync(c => c.ContractId == contractId);

        if (!exists)
        {
            throw new Exception("Contract khong ton tai.");
        }
    }

    private async Task EnsureCoordinatorExistsAsync(int coordinatorId)
    {
        var exists = await _uow.GetRepository<ServiceProviderCoordinator>().Entities
            .AsNoTracking()
            .AnyAsync(c => c.CoordinatorId == coordinatorId && c.IsActive);

        if (!exists)
        {
            throw new Exception("Service Provider khong ton tai hoac da bi khoa.");
        }
    }

    private async Task EnsureOptionalAreaExistsAsync(int? areaId)
    {
        if (!areaId.HasValue)
        {
            return;
        }

        var exists = await _uow.GetRepository<OperatingArea>().Entities
            .AsNoTracking()
            .AnyAsync(a => a.AreaId == areaId.Value && a.IsActive);

        if (!exists)
        {
            throw new Exception("Area khong ton tai hoac da bi khoa.");
        }
    }

    private async Task EnsureOptionalCategoryExistsAsync(int? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await _uow.GetRepository<UrbanServiceCategory>().Entities
            .AsNoTracking()
            .AnyAsync(c => c.CategoryId == categoryId.Value && c.IsActive);

        if (!exists)
        {
            throw new Exception("Category khong ton tai hoac da bi khoa.");
        }
    }

    private static ProviderContractDto MapContract(ProviderContract contract)
    {
        return new ProviderContractDto
        {
            ContractId = contract.ContractId,
            CoordinatorId = contract.CoordinatorId,
            ProviderName = contract.Coordinator?.ProviderName,
            AreaId = contract.AreaId,
            AreaName = contract.Area?.AreaName,
            CategoryId = contract.CategoryId,
            CategoryName = contract.Category?.CategoryName,
            ContractCode = contract.ContractCode,
            ContractName = contract.ContractName,
            Description = contract.Description,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            Status = contract.Status,
            CreatedByUserId = contract.CreatedByUserId,
            CreatedByUserName = contract.CreatedByUser?.FullName,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt,
            AttachmentCount = contract.ProviderContractAttachments.Count
        };
    }

    private static ProviderContractAttachmentDto MapAttachment(ProviderContractAttachment attachment)
    {
        return new ProviderContractAttachmentDto
        {
            ContractAttachmentId = attachment.ContractAttachmentId,
            ContractId = attachment.ContractId,
            FileUrl = attachment.FileUrl,
            FileType = attachment.FileType,
            Description = attachment.Description,
            UploadedByUserId = attachment.UploadedByUserId,
            UploadedByUserName = attachment.UploadedByUser?.FullName,
            UploadedAt = attachment.UploadedAt
        };
    }

    private static void ValidateContract(string contractCode, string contractName, DateOnly startDate)
    {
        if (string.IsNullOrWhiteSpace(contractCode))
        {
            throw new Exception("ContractCode la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(contractName))
        {
            throw new Exception("ContractName la bat buoc.");
        }

        if (startDate == default)
        {
            throw new Exception("StartDate la bat buoc.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

public class ProviderContractAttachmentUploadRequest
{
    public string? Description { get; set; }

    public List<IFormFile>? Files { get; set; }
}
