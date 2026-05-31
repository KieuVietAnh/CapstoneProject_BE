using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class Channel
{
    public int ChannelId { get; set; }

    public Guid FeedbackId { get; set; }

    public string ChannelName { get; set; } = null!;

    public string? ExternalConversationId { get; set; }

    public string? ExternalMessageId { get; set; }

    public string? SourceUserExternalId { get; set; }

    public DateTime ReceivedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;
}
