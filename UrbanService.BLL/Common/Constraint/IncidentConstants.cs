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
}

public static class IncidentSubscriptionSource
{
    public const string Report = "Report";
}
