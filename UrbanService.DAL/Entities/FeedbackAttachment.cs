using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackAttachment
{
    public int AttachmentId { get; set; }

    public Guid FeedbackId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;
}
