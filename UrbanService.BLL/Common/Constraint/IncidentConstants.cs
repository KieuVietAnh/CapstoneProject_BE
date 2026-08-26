namespace UrbanService.BLL.Common.Constraint;

public static class IncidentLinkStatus
{
    public const string Active = "Active";
    public const string Unlinked = "Unlinked";
}

public static class IncidentLinkMethod
{
    public const string Created = "Created";
    public const string UserSelected = "UserSelected";
    public const string StaffConfirmed = "StaffConfirmed";

    public static readonly IReadOnlyCollection<string> ManagementAllowed =
    [
        UserSelected,
        StaffConfirmed
    ];
}

public static class IncidentLinkRole
{
    public const string Primary = "Primary";
    public const string Corroborating = "Corroborating";
}

public static class IncidentEventType
{
    public const string IncidentCreated = "IncidentCreated";
    public const string ReportLinked = "ReportLinked";
    public const string ReportUnlinked = "ReportUnlinked";
    public const string IncidentMerged = "IncidentMerged";
    public const string IncidentUpdated = "IncidentUpdated";
    public const string StatusChanged = "StatusChanged";
    public const string AssigneeChanged = "AssigneeChanged";
}

public static class IncidentSubscriptionSource
{
    public const string Report = "Report";
    public const string Manual = "Manual";
}

public static class IncidentSeverity
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
    public const string Critical = "Critical";

    public static readonly IReadOnlyCollection<string> All = [Low, Medium, High, Critical];
}

public static class IncidentStatus
{
    public const string New = "New";
    public const string Verified = "Verified";
    public const string Assigned = "Assigned";
    public const string InProgress = "InProgress";
    public const string Resolved = "Resolved";
    public const string SubmittedForApproval = "SubmittedForApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string NeedRework = "NeedRework";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
    public const string Merged = "Merged";

    public static readonly IReadOnlyCollection<string> ManagementAllowed =
    [
        New,
        Verified,
        Assigned,
        InProgress,
        Resolved,
        SubmittedForApproval,
        Approved,
        Rejected,
        NeedRework,
        Closed,
        Cancelled
    ];
}
