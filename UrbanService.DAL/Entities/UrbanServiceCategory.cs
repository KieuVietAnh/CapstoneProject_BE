using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class UrbanServiceCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AiKnowledgeSource> AiKnowledgeSources { get; set; } = new List<AiKnowledgeSource>();

    public virtual ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<ServiceOperator> ServiceOperators { get; set; } = new List<ServiceOperator>();

    public virtual ICollection<Service> Services { get; set; } = new List<Service>();
}
