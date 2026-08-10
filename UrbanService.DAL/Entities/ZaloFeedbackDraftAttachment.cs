namespace UrbanService.DAL.Entities;

public class ZaloFeedbackDraftAttachment
{
    public long DraftAttachmentId { get; set; }

    public long ConversationId { get; set; }

    public string SourceUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ZaloFeedbackConversation Conversation { get; set; } = null!;
}
