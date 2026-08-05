using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.DTOs.SLA;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class SlaPolicyService : ISlaPolicyService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private readonly IUnitOfWork _unitOfWork;

    public SlaPolicyService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SlaPolicyDto> CreateAsync(
        Guid currentUserId,
        SlaPolicyCreateRequest request)
    {
        ValidateUserId(currentUserId);
        ValidateCreateRequest(request);

        var normalizedPriority = NormalizePriority(request.Priority);

        await EnsureUserExistsAsync(currentUserId);
        await EnsureAreaExistsAsync(request.AreaId);
        await EnsureCategoryExistsAsync(request.CategoryId);

        if (request.IsActive)
        {
            await EnsureNoOverlappingPolicyAsync(
                currentSlaPolicyId: null,
                areaId: request.AreaId,
                categoryId: request.CategoryId,
                priority: normalizedPriority,
                effectiveFrom: request.EffectiveFrom,
                effectiveTo: request.EffectiveTo);
        }

        var entity = new SlaPolicy
        {
            PolicyName = request.PolicyName.Trim(),
            AreaId = request.AreaId,
            CategoryId = request.CategoryId,
            Priority = normalizedPriority,
            ResponseTimeMinutes = request.ResponseTimeMinutes,
            ResolutionTimeMinutes = request.ResolutionTimeMinutes,
            EffectiveFrom = ToDbTime(request.EffectiveFrom),
            EffectiveTo = ToDbTime(request.EffectiveTo),
            IsActive = request.IsActive,
            CreatedByUserId = currentUserId,
            UpdatedByUserId = null,
            CreatedAt = DbNow(),
            UpdatedAt = null
        };

        await _unitOfWork
            .GetRepository<SlaPolicy>()
            .AddAsync(entity);

        await _unitOfWork.SaveAsync();

        return await GetByIdAsync(entity.SlaPolicyId);
    }

    public async Task<SlaPolicyDto> UpdateAsync(
        Guid currentUserId,
        int slaPolicyId,
        SlaPolicyUpdateRequest request)
    {
        ValidateUserId(currentUserId);
        ValidateSlaPolicyId(slaPolicyId);
        ValidateUpdateRequest(request);

        var normalizedPriority = NormalizePriority(request.Priority);

        await EnsureUserExistsAsync(currentUserId);
        await EnsureAreaExistsAsync(request.AreaId);
        await EnsureCategoryExistsAsync(request.CategoryId);

        var policyRepository =
            _unitOfWork.GetRepository<SlaPolicy>();

        var entity = await policyRepository.Entities
            .FirstOrDefaultAsync(x =>
                x.SlaPolicyId == slaPolicyId);

        if (entity == null)
        {
            throw new KeyNotFoundException(
                $"Không tìm thấy SLA policy có ID {slaPolicyId}.");
        }

        if (request.IsActive)
        {
            await EnsureNoOverlappingPolicyAsync(
                currentSlaPolicyId: slaPolicyId,
                areaId: request.AreaId,
                categoryId: request.CategoryId,
                priority: normalizedPriority,
                effectiveFrom: request.EffectiveFrom,
                effectiveTo: request.EffectiveTo);
        }

        entity.PolicyName = request.PolicyName.Trim();
        entity.AreaId = request.AreaId;
        entity.CategoryId = request.CategoryId;
        entity.Priority = normalizedPriority;
        entity.ResponseTimeMinutes = request.ResponseTimeMinutes;
        entity.ResolutionTimeMinutes = request.ResolutionTimeMinutes;
        entity.EffectiveFrom = ToDbTime(request.EffectiveFrom);
        entity.EffectiveTo = ToDbTime(request.EffectiveTo);
        entity.IsActive = request.IsActive;
        entity.UpdatedByUserId = currentUserId;
        entity.UpdatedAt = DbNow();

        await _unitOfWork.SaveAsync();

        return await GetByIdAsync(entity.SlaPolicyId);
    }

    public async Task<SlaPolicyDto> GetByIdAsync(
        int slaPolicyId)
    {
        ValidateSlaPolicyId(slaPolicyId);

        var now = DbNow();

        var result = await _unitOfWork
            .GetRepository<SlaPolicy>()
            .Entities
            .AsNoTracking()
            .Where(x => x.SlaPolicyId == slaPolicyId)
            .Select(x => new SlaPolicyDto
            {
                SlaPolicyId = x.SlaPolicyId,
                PolicyName = x.PolicyName,

                AreaId = x.AreaId,
                AreaName = x.Area != null
                    ? x.Area.AreaName
                    : null,

                CategoryId = x.CategoryId,
                CategoryName = x.Category != null
                    ? x.Category.CategoryName
                    : null,

                Priority = x.Priority,
                ResponseTimeMinutes = x.ResponseTimeMinutes,
                ResolutionTimeMinutes = x.ResolutionTimeMinutes,

                EffectiveFrom = x.EffectiveFrom,
                EffectiveTo = x.EffectiveTo,

                IsActive = x.IsActive,

                IsCurrentlyEffective =
                    x.IsActive &&
                    x.EffectiveFrom <= now &&
                    (
                        !x.EffectiveTo.HasValue ||
                        x.EffectiveTo.Value >= now
                    ),

                CreatedByUserId = x.CreatedByUserId,
                UpdatedByUserId = x.UpdatedByUserId,

                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (result == null)
        {
            throw new KeyNotFoundException(
                $"Không tìm thấy SLA policy có ID {slaPolicyId}.");
        }

        return result;
    }

    public async Task<PagedResultDto<SlaPolicyDto>> GetAllAsync(
        SlaPolicyQueryParameters query)
    {
        query ??= new SlaPolicyQueryParameters();

        var pageNumber = query.PageNumber <= 0
            ? DefaultPageNumber
            : query.PageNumber;

        var pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaxPageSize);

        var now = DbNow();

        var policyQuery = _unitOfWork
            .GetRepository<SlaPolicy>()
            .Entities
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();

            policyQuery = policyQuery.Where(x =>
                x.PolicyName.ToLower().Contains(search));
        }

        if (query.AreaId.HasValue)
        {
            policyQuery = policyQuery.Where(x =>
                x.AreaId == query.AreaId.Value);
        }

        if (query.CategoryId.HasValue)
        {
            policyQuery = policyQuery.Where(x =>
                x.CategoryId == query.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            var normalizedPriority =
                NormalizePriority(query.Priority);

            policyQuery = policyQuery.Where(x =>
                x.Priority == normalizedPriority);
        }

        if (query.IsActive.HasValue)
        {
            policyQuery = policyQuery.Where(x =>
                x.IsActive == query.IsActive.Value);
        }

        if (query.IsCurrentlyEffective == true)
        {
            policyQuery = policyQuery.Where(x =>
                x.IsActive &&
                x.EffectiveFrom <= now &&
                (
                    !x.EffectiveTo.HasValue ||
                    x.EffectiveTo.Value >= now
                ));
        }
        else if (query.IsCurrentlyEffective == false)
        {
            policyQuery = policyQuery.Where(x =>
                !x.IsActive ||
                x.EffectiveFrom > now ||
                (
                    x.EffectiveTo.HasValue &&
                    x.EffectiveTo.Value < now
                ));
        }

        var totalItems = await policyQuery.CountAsync();

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(
                totalItems / (double)pageSize);

        var items = await policyQuery
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SlaPolicyDto
            {
                SlaPolicyId = x.SlaPolicyId,
                PolicyName = x.PolicyName,

                AreaId = x.AreaId,
                AreaName = x.Area != null
                    ? x.Area.AreaName
                    : null,

                CategoryId = x.CategoryId,
                CategoryName = x.Category != null
                    ? x.Category.CategoryName
                    : null,

                Priority = x.Priority,
                ResponseTimeMinutes = x.ResponseTimeMinutes,
                ResolutionTimeMinutes = x.ResolutionTimeMinutes,

                EffectiveFrom = x.EffectiveFrom,
                EffectiveTo = x.EffectiveTo,

                IsActive = x.IsActive,

                IsCurrentlyEffective =
                    x.IsActive &&
                    x.EffectiveFrom <= now &&
                    (
                        !x.EffectiveTo.HasValue ||
                        x.EffectiveTo.Value >= now
                    ),

                CreatedByUserId = x.CreatedByUserId,
                UpdatedByUserId = x.UpdatedByUserId,

                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return new PagedResultDto<SlaPolicyDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task SetActiveAsync(
        Guid currentUserId,
        int slaPolicyId,
        bool isActive)
    {
        ValidateUserId(currentUserId);
        ValidateSlaPolicyId(slaPolicyId);

        await EnsureUserExistsAsync(currentUserId);

        var policyRepository =
            _unitOfWork.GetRepository<SlaPolicy>();

        var entity = await policyRepository.Entities
            .FirstOrDefaultAsync(x =>
                x.SlaPolicyId == slaPolicyId);

        if (entity == null)
        {
            throw new KeyNotFoundException(
                $"Không tìm thấy SLA policy có ID {slaPolicyId}.");
        }

        if (entity.IsActive == isActive)
        {
            return;
        }

        if (isActive)
        {
            await EnsureNoOverlappingPolicyAsync(
                currentSlaPolicyId: entity.SlaPolicyId,
                areaId: entity.AreaId,
                categoryId: entity.CategoryId,
                priority: entity.Priority,
                effectiveFrom: entity.EffectiveFrom,
                effectiveTo: entity.EffectiveTo);
        }

        entity.IsActive = isActive;
        entity.UpdatedByUserId = currentUserId;
        entity.UpdatedAt = DbNow();

        await _unitOfWork.SaveAsync();
    }

    public async Task DeleteAsync(int slaPolicyId)
    {
        ValidateSlaPolicyId(slaPolicyId);

        var policyRepository =
            _unitOfWork.GetRepository<SlaPolicy>();

        var entity = await policyRepository.Entities
            .FirstOrDefaultAsync(x =>
                x.SlaPolicyId == slaPolicyId);

        if (entity == null)
        {
            throw new KeyNotFoundException(
                $"Không tìm thấy SLA policy có ID {slaPolicyId}.");
        }

        var hasBeenUsed = await _unitOfWork
            .GetRepository<FeedbackSla>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x =>
                x.SlaPolicyId == slaPolicyId);

        if (hasBeenUsed)
        {
            throw new InvalidOperationException(
                "Không thể xóa SLA policy đã được áp dụng cho feedback. " +
                "Hãy chuyển policy sang trạng thái không hoạt động.");
        }

        policyRepository.Delete(entity);

        await _unitOfWork.SaveAsync();
    }

    private async Task EnsureUserExistsAsync(Guid userId)
    {
        var exists = await _unitOfWork
            .GetRepository<User>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "Không tìm thấy người dùng thực hiện thao tác.");
        }
    }

    private async Task EnsureAreaExistsAsync(int? areaId)
    {
        if (!areaId.HasValue)
        {
            return;
        }

        var exists = await _unitOfWork
            .GetRepository<OperatingArea>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x =>
                x.AreaId == areaId.Value &&
                x.IsActive);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "Khu vực không tồn tại hoặc không còn hoạt động.");
        }
    }

    private async Task EnsureCategoryExistsAsync(
        int? categoryId)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await _unitOfWork
            .GetRepository<UrbanServiceCategory>()
            .Entities
            .AsNoTracking()
            .AnyAsync(x =>
                x.CategoryId == categoryId.Value &&
                x.IsActive);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "Category không tồn tại hoặc không còn hoạt động.");
        }
    }

    private async Task EnsureNoOverlappingPolicyAsync(
        int? currentSlaPolicyId,
        int? areaId,
        int? categoryId,
        string priority,
        DateTime effectiveFrom,
        DateTime? effectiveTo)
    {
        effectiveFrom = ToDbTime(effectiveFrom);
        effectiveTo = ToDbTime(effectiveTo);

        var query = _unitOfWork
            .GetRepository<SlaPolicy>()
            .Entities
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.AreaId == areaId &&
                x.CategoryId == categoryId &&
                x.Priority == priority);

        if (currentSlaPolicyId.HasValue)
        {
            query = query.Where(x =>
                x.SlaPolicyId != currentSlaPolicyId.Value);
        }

        var hasOverlap = await query.AnyAsync(x =>
            (
                !x.EffectiveTo.HasValue ||
                x.EffectiveTo.Value >= effectiveFrom
            )
            &&
            (
                !effectiveTo.HasValue ||
                x.EffectiveFrom <= effectiveTo.Value
            ));

        if (hasOverlap)
        {
            throw new InvalidOperationException(
                "Đã tồn tại SLA policy đang hoạt động có cùng khu vực, " +
                "category, priority và bị chồng lấn thời gian hiệu lực.");
        }
    }


    private static DateTime DbNow()
    {
        return DateTime.SpecifyKind(
            DateTime.UtcNow,
            DateTimeKind.Unspecified);
    }

    private static DateTime ToDbTime(DateTime value)
    {
        return DateTime.SpecifyKind(
            value,
            DateTimeKind.Unspecified);
    }

    private static DateTime? ToDbTime(DateTime? value)
    {
        return value.HasValue
            ? DateTime.SpecifyKind(
                value.Value,
                DateTimeKind.Unspecified)
            : null;
    }

    private static void ValidateCreateRequest(
        SlaPolicyCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateCommonRequest(
            policyName: request.PolicyName,
            priority: request.Priority,
            responseTimeMinutes: request.ResponseTimeMinutes,
            resolutionTimeMinutes: request.ResolutionTimeMinutes,
            effectiveFrom: request.EffectiveFrom,
            effectiveTo: request.EffectiveTo);
    }

    private static void ValidateUpdateRequest(
        SlaPolicyUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateCommonRequest(
            policyName: request.PolicyName,
            priority: request.Priority,
            responseTimeMinutes: request.ResponseTimeMinutes,
            resolutionTimeMinutes: request.ResolutionTimeMinutes,
            effectiveFrom: request.EffectiveFrom,
            effectiveTo: request.EffectiveTo);
    }

    private static void ValidateCommonRequest(
        string policyName,
        string priority,
        int responseTimeMinutes,
        int resolutionTimeMinutes,
        DateTime effectiveFrom,
        DateTime? effectiveTo)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException(
                "Tên SLA policy là bắt buộc.");
        }

        if (policyName.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Tên SLA policy không được vượt quá 200 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException(
                "Priority là bắt buộc.");
        }

        if (responseTimeMinutes <= 0)
        {
            throw new ArgumentException(
                "Thời gian phản hồi phải lớn hơn 0 phút.");
        }

        if (resolutionTimeMinutes <= 0)
        {
            throw new ArgumentException(
                "Thời gian xử lý phải lớn hơn 0 phút.");
        }

        if (resolutionTimeMinutes < responseTimeMinutes)
        {
            throw new ArgumentException(
                "Thời gian xử lý không được nhỏ hơn " +
                "thời gian phản hồi.");
        }

        if (effectiveFrom == default)
        {
            throw new ArgumentException(
                "Thời điểm bắt đầu hiệu lực là bắt buộc.");
        }

        if (effectiveTo.HasValue &&
            effectiveTo.Value <= effectiveFrom)
        {
            throw new ArgumentException(
                "Thời điểm kết thúc hiệu lực phải sau " +
                "thời điểm bắt đầu hiệu lực.");
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID không hợp lệ.");
        }
    }

    private static void ValidateSlaPolicyId(
        int slaPolicyId)
    {
        if (slaPolicyId <= 0)
        {
            throw new ArgumentException(
                "SLA policy ID không hợp lệ.");
        }
    }

    private static string NormalizePriority(
        string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException(
                "Priority là bắt buộc.");
        }

        var normalized = priority.Trim();

        if (normalized.Equals(
                "Low",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Low";
        }

        if (normalized.Equals(
                "Medium",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Medium";
        }

        if (normalized.Equals(
                "High",
                StringComparison.OrdinalIgnoreCase))
        {
            return "High";
        }

        if (normalized.Equals(
            "Urgent",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Urgent";
        }

        throw new ArgumentException(
            "Priority không hợp lệ. " +
            "Chỉ chấp nhận Low, Medium, High hoặc Urgent.");
    }
}