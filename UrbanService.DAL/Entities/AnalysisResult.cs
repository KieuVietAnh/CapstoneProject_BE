using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class AnalysisResult
{
    public int AnalysisResultId { get; set; }

    public Guid FeedbackId { get; set; }

    public string? ModelName { get; set; }

    public int? DetectedCategoryId { get; set; }

    public string? Sentiment { get; set; }

    public string? UrgencyLevel { get; set; }

    public string? Summary { get; set; }

    public string? Keywords { get; set; }

    public decimal? ConfidenceScore { get; set; }

    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual UrbanServiceCategory? DetectedCategory { get; set; }

    public virtual Feedback Feedback { get; set; } = null!;
}
