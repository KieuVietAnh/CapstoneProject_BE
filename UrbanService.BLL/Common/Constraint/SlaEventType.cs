namespace UrbanService.BLL.Common.Constraint;

public static class SlaEventType
{
    public const string Started = "Started";
    public const string Responded = "Responded";
    public const string Paused = "Paused";
    public const string Resumed = "Resumed";

    public const string ResponseWarning = "ResponseWarning";
    public const string ResolutionWarning = "ResolutionWarning";

    public const string ResponseBreached = "ResponseBreached";
    public const string ResolutionBreached = "ResolutionBreached";

    public const string Recalculated = "Recalculated";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}