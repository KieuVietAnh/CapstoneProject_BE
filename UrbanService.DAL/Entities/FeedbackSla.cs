using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackSla
{
    public long FeedbackSlaId { get; set; }

    public Guid FeedbackId { get; set; }

    public int SlaPolicyId { get; set; }

    /// <summary>
    /// Snapshot khu vực tại thời điểm SLA được tạo.
    /// </summary>
    public int AreaId { get; set; }

    /// <summary>
    /// Snapshot category tại thời điểm SLA được tạo.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Snapshot priority tại thời điểm SLA được tạo.
    /// </summary>
    public string Priority { get; set; } = null!;

    /// <summary>
    /// Thời điểm staff xác nhận feedback và SLA bắt đầu chạy.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Hạn phản hồi lần đầu.
    /// </summary>
    public DateTime ResponseDueAt { get; set; }

    /// <summary>
    /// Hạn hoàn thành xử lý.
    /// </summary>
    public DateTime ResolutionDueAt { get; set; }

    /// <summary>
    /// Thời điểm đã có phản hồi đầu tiên.
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Thời điểm feedback được hoàn thành.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Tổng số phút SLA đã bị tạm dừng.
    /// </summary>
    public int TotalPausedMinutes { get; set; }

    /// <summary>
    /// Running, Paused, Completed hoặc Cancelled.
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Pending, Met hoặc Breached.
    /// </summary>
    public string ResponseStatus { get; set; } = null!;

    /// <summary>
    /// Pending, Met hoặc Breached.
    /// </summary>
    public string ResolutionStatus { get; set; } = null!;

    public bool IsResponseBreached { get; set; }

    public bool IsResolutionBreached { get; set; }

    /// <summary>
    /// Đánh dấu đây là SLA hiện tại của feedback.
    /// Các SLA cũ có IsCurrent = false.
    /// </summary>
    public bool IsCurrent { get; set; }

    public Guid StartedByUserId { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual SlaPolicy SlaPolicy { get; set; } = null!;

    public virtual OperatingArea Area { get; set; } = null!;

    public virtual UrbanServiceCategory Category { get; set; } = null!;

    public virtual User StartedByUser { get; set; } = null!;

    public virtual User? CompletedByUser { get; set; }

    public virtual ICollection<SlaEvent> SlaEvents { get; set; }
        = new List<SlaEvent>();

    public virtual ICollection<SlaPauseHistory> SlaPauseHistories { get; set; }
        = new List<SlaPauseHistory>();
}