namespace UrbanService.DAL.Entities;

public class MessengerFeedbackDraftAttachment
{
    public long DraftAttachmentId { get; set; }

    public long ConversationId { get; set; }

    public string SourceUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public string SourceMessageId { get; set; } = null!;

    public int SourceOrdinal { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual MessengerFeedbackConversation Conversation { get; set; } = null!;
}
