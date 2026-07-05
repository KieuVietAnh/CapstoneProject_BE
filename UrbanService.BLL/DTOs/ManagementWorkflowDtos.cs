namespace UrbanService.BLL.DTOs;

public class CategoryDto
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CategoryCreateRequest
{
    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }
}

public class CategoryUpdateRequest
{
    public string? CategoryName { get; set; }

    public string? Description { get; set; }
}

public class AreaDto
{
    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public string AreaType { get; set; } = null!;

    public string? WardCode { get; set; }

    public string? DistrictName { get; set; }

    public string? ProvinceName { get; set; }

    public decimal? CenterLatitude { get; set; }

    public decimal? CenterLongitude { get; set; }

    public string? BoundaryGeoJson { get; set; }

    public bool IsActive { get; set; }

    public DateOnly? StartedAt { get; set; }

    public DateOnly? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class AreaCreateRequest
{
    public string AreaName { get; set; } = null!;

    public string AreaType { get; set; } = null!;

    public string? WardCode { get; set; }

    public string? DistrictName { get; set; }

    public string? ProvinceName { get; set; }

    public decimal? CenterLatitude { get; set; }

    public decimal? CenterLongitude { get; set; }

    public string? BoundaryGeoJson { get; set; }

    public DateOnly? StartedAt { get; set; }

    public DateOnly? EndedAt { get; set; }
}

public class AreaUpdateRequest
{
    public string? AreaName { get; set; }

    public string? AreaType { get; set; }

    public string? WardCode { get; set; }

    public string? DistrictName { get; set; }

    public string? ProvinceName { get; set; }

    public decimal? CenterLatitude { get; set; }

    public decimal? CenterLongitude { get; set; }

    public string? BoundaryGeoJson { get; set; }

    public DateOnly? StartedAt { get; set; }

    public DateOnly? EndedAt { get; set; }
}

public class SetActiveRequest
{
    public bool IsActive { get; set; }
}

public class ProviderCandidateDto
{
    public int CoordinatorId { get; set; }

    public string ProviderName { get; set; } = null!;

    public string CoordinatorName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Note { get; set; }

    public bool IsPrimary { get; set; }

    public int PriorityOrder { get; set; }

    public int? ContractId { get; set; }

    public string? ContractCode { get; set; }

    public string? ContractName { get; set; }

    public string? ContractStatus { get; set; }
}

public class FeedbackProviderReportDto
{
    public int ProviderReportId { get; set; }

    public Guid FeedbackId { get; set; }

    public int CoordinatorId { get; set; }

    public string? ProviderName { get; set; }

    public string? CoordinatorName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public Guid ReportedByUserId { get; set; }

    public string? ReportedByUserName { get; set; }

    public string ReportStatus { get; set; } = null!;

    public DateTime? DueDate { get; set; }

    public string? ReportNote { get; set; }

    public DateTime ReportedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int ContactLogCount { get; set; }

    public int CompletionDocumentCount { get; set; }
}

public class UpdateProviderReportStatusRequest
{
    public string Status { get; set; } = null!;

    public string? Note { get; set; }
}

public class ProviderContactLogCreateRequest
{
    public string ContactMethod { get; set; } = null!;

    public string? ContactResult { get; set; }

    public string? ContactNote { get; set; }

    public DateTime? ContactedAt { get; set; }
}

public class ProviderContactLogDto
{
    public int ContactLogId { get; set; }

    public int ProviderReportId { get; set; }

    public int CoordinatorId { get; set; }

    public string? ProviderName { get; set; }

    public string? CoordinatorName { get; set; }

    public Guid ContactedByUserId { get; set; }

    public string? ContactedByUserName { get; set; }

    public string ContactMethod { get; set; } = null!;

    public string? ContactResult { get; set; }

    public string? ContactNote { get; set; }

    public DateTime ContactedAt { get; set; }
}

public class CompletionDocumentDto
{
    public int CompletionDocumentId { get; set; }

    public int ProviderReportId { get; set; }

    public Guid FeedbackId { get; set; }

    public int CoordinatorId { get; set; }

    public string? ProviderName { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string? UploadedByUserName { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public string? Description { get; set; }

    public DateTime ReceivedAt { get; set; }
}

public class FeedbackResolutionDto
{
    public int ResolutionId { get; set; }

    public Guid FeedbackId { get; set; }

    public int? ProviderReportId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public string? CreatedByStaffUserName { get; set; }

    public string ResolutionSummary { get; set; } = null!;

    public string ActionTaken { get; set; } = null!;

    public string? ResultNote { get; set; }

    public DateTime ResolvedAt { get; set; }

    public string Status { get; set; } = null!;
}

public class FeedbackResolutionReviewDto
{
    public int ReviewId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public string? UserName { get; set; }

    public int Rating { get; set; }

    public bool IsSatisfied { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class CitizenResolutionReviewRequest
{
    public int Rating { get; set; }

    public bool IsSatisfied { get; set; }

    public string? Comment { get; set; }
}

public class NotifyProviderResultRequest
{
    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? TargetUrl { get; set; }
}

public class ServiceProviderCoordinatorDto
{
    public int CoordinatorId { get; set; }

    public string ProviderName { get; set; } = null!;

    public string CoordinatorName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int CoverageCount { get; set; }
}

public class ServiceProviderCoordinatorCreateRequest
{
    public string ProviderName { get; set; } = null!;

    public string CoordinatorName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Note { get; set; }
}

public class ServiceProviderCoordinatorUpdateRequest
{
    public string? ProviderName { get; set; }

    public string? CoordinatorName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Note { get; set; }
}

public class CoordinatorCoverageDto
{
    public int CoverageId { get; set; }

    public int CoordinatorId { get; set; }

    public int AreaId { get; set; }

    public string? AreaName { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public bool IsPrimary { get; set; }

    public int PriorityOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CoordinatorCoverageCreateRequest
{
    public int AreaId { get; set; }

    public int CategoryId { get; set; }

    public bool IsPrimary { get; set; }

    public int PriorityOrder { get; set; } = 1;
}

public class CoordinatorCoverageUpdateRequest
{
    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public bool? IsPrimary { get; set; }

    public int? PriorityOrder { get; set; }

    public bool? IsActive { get; set; }
}

public class ProviderContractDto
{
    public int ContractId { get; set; }

    public int CoordinatorId { get; set; }

    public string? ProviderName { get; set; }

    public int? AreaId { get; set; }

    public string? AreaName { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string ContractCode { get; set; } = null!;

    public string ContractName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }

    public string? CreatedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int AttachmentCount { get; set; }
}

public class ProviderContractCreateRequest
{
    public int CoordinatorId { get; set; }

    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string ContractCode { get; set; } = null!;

    public string ContractName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Status { get; set; }
}

public class ProviderContractUpdateRequest
{
    public int? CoordinatorId { get; set; }

    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string? ContractCode { get; set; }

    public string? ContractName { get; set; }

    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Status { get; set; }
}

public class ProviderContractAttachmentDto
{
    public int ContractAttachmentId { get; set; }

    public int ContractId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public string? Description { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string? UploadedByUserName { get; set; }

    public DateTime UploadedAt { get; set; }
}

public class StaffAreaAssignmentDto
{
    public int StaffAreaAssignmentId { get; set; }

    public Guid UserId { get; set; }

    public string? StaffName { get; set; }

    public int AreaId { get; set; }

    public string? AreaName { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public string? AssignedByUserName { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class StaffAreaAssignmentCreateRequest
{
    public Guid UserId { get; set; }

    public int AreaId { get; set; }

    public bool IsPrimary { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
}
