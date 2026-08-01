using System;

namespace UrbanService.DAL.Entities;

public partial class SlaPauseHistory
{
    public long SlaPauseHistoryId { get; set; }

    public long FeedbackSlaId { get; set; }

    /// <summary>
    /// WaitingCitizen, ForceMajeure, ExternalDependency,
    /// SystemMaintenance hoặc Other.
    /// </summary>
    public string ReasonCode { get; set; } = null!;

    public string? ReasonNote { get; set; }

    public DateTime PausedAt { get; set; }

    public DateTime? ResumedAt { get; set; }

    /// <summary>
    /// Tổng số phút của lần pause này.
    /// Chỉ có giá trị sau khi resume.
    /// </summary>
    public int? PausedMinutes { get; set; }

    public Guid PausedByUserId { get; set; }

    public Guid? ResumedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual FeedbackSla FeedbackSla { get; set; } = null!;

    public virtual User PausedByUser { get; set; } = null!;

    public virtual User? ResumedByUser { get; set; }
}