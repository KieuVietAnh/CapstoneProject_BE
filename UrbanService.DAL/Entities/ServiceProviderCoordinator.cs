using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class ServiceProviderCoordinator
{
    public int CoordinatorId { get; set; }

    public string ProviderName { get; set; } = null!;

    public string CoordinatorName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CompletionDocument> CompletionDocuments { get; set; } = new List<CompletionDocument>();

    public virtual ICollection<CoordinatorCoverage> CoordinatorCoverages { get; set; } = new List<CoordinatorCoverage>();

    public virtual ICollection<FeedbackProviderReport> FeedbackProviderReports { get; set; } = new List<FeedbackProviderReport>();

    public virtual ICollection<ProviderContactLog> ProviderContactLogs { get; set; } = new List<ProviderContactLog>();

    public virtual ICollection<ProviderContract> ProviderContracts { get; set; } = new List<ProviderContract>();
}
