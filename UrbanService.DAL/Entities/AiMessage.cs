using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class AiMessage
{
    public int AiMessageId { get; set; }

    public int AiConversationId { get; set; }

    public string SenderType { get; set; } = null!;

    public string MessageText { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual AiConversation AiConversation { get; set; } = null!;
}
