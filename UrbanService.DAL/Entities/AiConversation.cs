using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class AiConversation
{
    public int AiConversationId { get; set; }

    public Guid UserId { get; set; }

    public Guid? FeedbackId { get; set; }

    public string? Title { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<AiMessage> AiMessages { get; set; } = new List<AiMessage>();

    public virtual Feedback? Feedback { get; set; }

    public virtual User User { get; set; } = null!;
}
