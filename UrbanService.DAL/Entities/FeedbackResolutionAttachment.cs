using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class FeedbackResolutionAttachment
{
    public int ResolutionAttachmentId { get; set; }

    public int ResolutionId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual FeedbackResolution Resolution { get; set; } = null!;
}
