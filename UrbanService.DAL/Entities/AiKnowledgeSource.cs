using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class AiKnowledgeSource
{
    public int KnowledgeSourceId { get; set; }

    public int? CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public string? Content { get; set; }

    public string? FileUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual UrbanServiceCategory? Category { get; set; }
}
