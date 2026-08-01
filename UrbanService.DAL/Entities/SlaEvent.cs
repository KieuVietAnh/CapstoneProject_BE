using System;

namespace UrbanService.DAL.Entities;

public partial class SlaEvent
{
    public long SlaEventId { get; set; }

    public long FeedbackSlaId { get; set; }

    /// <summary>
    /// Started, Responded, Paused, Resumed, Warning,
    /// ResponseBreached, ResolutionBreached,
    /// Recalculated, Completed hoặc Cancelled.
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Trạng thái SLA trước khi phát sinh sự kiện.
    /// </summary>
    public string? OldStatus { get; set; }

    /// <summary>
    /// Trạng thái SLA sau khi phát sinh sự kiện.
    /// </summary>
    public string? NewStatus { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Null nếu sự kiện được tạo tự động bởi background worker.
    /// </summary>
    public Guid? TriggeredByUserId { get; set; }

    /// <summary>
    /// User hoặc System.
    /// </summary>
    public string TriggerSource { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual FeedbackSla FeedbackSla { get; set; } = null!;

    public virtual User? TriggeredByUser { get; set; }
}