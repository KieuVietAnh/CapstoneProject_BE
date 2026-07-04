using System;

namespace UrbanService.DAL.Entities;

public partial class ProviderContractAttachment
{
    public int ContractAttachmentId { get; set; }

    public int ContractId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public string? Description { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual ProviderContract Contract { get; set; } = null!;

    public virtual User UploadedByUser { get; set; } = null!;
}
