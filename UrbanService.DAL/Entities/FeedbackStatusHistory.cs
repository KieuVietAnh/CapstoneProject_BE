using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackStatusHistory
{
    public int HistoryId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid ChangedByUserId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }

    public virtual User ChangedByUser { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;
}
