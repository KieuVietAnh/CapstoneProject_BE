using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class InteractionMessage
{
    public int InteractionMessageId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public string SenderType { get; set; } = null!;

    public string MessageText { get; set; } = null!;

    public bool IsInternal { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;

    public virtual ICollection<MessageAttachment> MessageAttachments { get; set; } = new List<MessageAttachment>();

    public virtual User User { get; set; } = null!;
}
