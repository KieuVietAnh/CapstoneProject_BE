using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackAssignment
{
    public int AssignmentId { get; set; }

    public Guid FeedbackId { get; set; }

    public int OperatorId { get; set; }

    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAt { get; set; }

    public string AssignmentStatus { get; set; } = null!;

    public string? Note { get; set; }

    public virtual User AssignedByUser { get; set; } = null!;

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual ServiceOperator Operator { get; set; } = null!;
}
