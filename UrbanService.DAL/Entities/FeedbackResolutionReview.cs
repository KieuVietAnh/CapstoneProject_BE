using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackResolutionReview
{
    public int ReviewId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public int? Rating { get; set; }

    public bool? IsSatisfied { get; set; }

    public string Comment { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
