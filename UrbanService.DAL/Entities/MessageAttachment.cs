using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class MessageAttachment
{
    public int MessageAttachmentId { get; set; }

    public int InteractionMessageId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual InteractionMessage InteractionMessage { get; set; } = null!;
}
