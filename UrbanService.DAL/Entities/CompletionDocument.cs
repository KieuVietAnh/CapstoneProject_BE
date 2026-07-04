using System;

namespace UrbanService.DAL.Entities;

public partial class CompletionDocument
{
    public int CompletionDocumentId { get; set; }

    public int ProviderReportId { get; set; }

    public Guid FeedbackId { get; set; }

    public int CoordinatorId { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public string? Description { get; set; }

    public DateTime ReceivedAt { get; set; }

    public virtual ServiceProviderCoordinator Coordinator { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual FeedbackProviderReport ProviderReport { get; set; } = null!;

    public virtual User UploadedByUser { get; set; } = null!;
}
