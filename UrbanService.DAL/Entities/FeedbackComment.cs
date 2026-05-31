using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackComment
{
    public int CommentId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
