using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackProviderReport
{
    public int ProviderReportId { get; set; }

    public Guid FeedbackId { get; set; }

    public int CoordinatorId { get; set; }

    public Guid ReportedByUserId { get; set; }

    public string ReportStatus { get; set; } = null!;

    public DateTime? DueDate { get; set; }

    public string? ReportNote { get; set; }

    public DateTime ReportedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ServiceProviderCoordinator Coordinator { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual User ReportedByUser { get; set; } = null!;

    public virtual ICollection<CompletionDocument> CompletionDocuments { get; set; } = new List<CompletionDocument>();

    public virtual ICollection<FeedbackResolution> FeedbackResolutions { get; set; } = new List<FeedbackResolution>();

    public virtual ICollection<ProviderContactLog> ProviderContactLogs { get; set; } = new List<ProviderContactLog>();
}
