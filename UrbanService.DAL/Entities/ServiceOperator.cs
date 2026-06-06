using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class ServiceOperator
{
    public int OperatorId { get; set; }

    public int CategoryId { get; set; }

    public string OperatorName { get; set; } = null!;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual UrbanServiceCategory Category { get; set; } = null!;

    public virtual ICollection<FeedbackAssignment> FeedbackAssignments { get; set; } = new List<FeedbackAssignment>();

    public virtual ICollection<FeedbackResolution> FeedbackResolutions { get; set; } = new List<FeedbackResolution>();

    public virtual ICollection<Service> Services { get; set; } = new List<Service>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
