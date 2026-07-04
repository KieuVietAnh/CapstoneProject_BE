using System;

namespace UrbanService.DAL.Entities;

public partial class ProviderContactLog
{
    public int ContactLogId { get; set; }

    public int ProviderReportId { get; set; }

    public int CoordinatorId { get; set; }

    public Guid ContactedByUserId { get; set; }

    public string ContactMethod { get; set; } = null!;

    public string? ContactResult { get; set; }

    public string? ContactNote { get; set; }

    public DateTime ContactedAt { get; set; }

    public virtual User ContactedByUser { get; set; } = null!;

    public virtual ServiceProviderCoordinator Coordinator { get; set; } = null!;

    public virtual FeedbackProviderReport ProviderReport { get; set; } = null!;
}
