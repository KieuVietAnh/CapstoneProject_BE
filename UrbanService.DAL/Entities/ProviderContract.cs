using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class ProviderContract
{
    public int ContractId { get; set; }

    public int CoordinatorId { get; set; }

    public int? AreaId { get; set; }

    public int? CategoryId { get; set; }

    public string ContractCode { get; set; } = null!;

    public string ContractName { get; set; } = null!;

    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual OperatingArea? Area { get; set; }

    public virtual UrbanServiceCategory? Category { get; set; }

    public virtual ServiceProviderCoordinator Coordinator { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<ProviderContractAttachment> ProviderContractAttachments { get; set; } = new List<ProviderContractAttachment>();
}
