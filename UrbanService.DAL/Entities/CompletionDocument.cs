using System;

namespace UrbanService.DAL.Entities;

public partial class CompletionDocument
{
    public int CompletionDocumentId { get; set; }

    /// <summary>
    /// Null khi sự vụ do Staff tự xử lý, không qua đơn vị bên thứ ba. Minh chứng
    /// khi đó chỉ gắn với Incident.
    /// </summary>
    public int? ProviderReportId { get; set; }

    public Guid IncidentId { get; set; }

    /// <summary>Null cùng lúc với <see cref="ProviderReportId"/>.</summary>
    public int? CoordinatorId { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public string? Description { get; set; }

    public DateTime ReceivedAt { get; set; }

    public virtual ServiceProviderCoordinator? Coordinator { get; set; }

    public virtual Incident Incident { get; set; } = null!;

    public virtual FeedbackProviderReport? ProviderReport { get; set; }

    public virtual User UploadedByUser { get; set; } = null!;
}
