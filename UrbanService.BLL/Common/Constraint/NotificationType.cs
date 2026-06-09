namespace UrbanService.BLL.Common.Constraint;

public static class NotificationType
{
    public const string TicketUpdated = "TicketUpdated";
    public const string Assignment = "Assignment";
    public const string Resolution = "Resolution";
    public const string System = "System";
    public const string Message = "Message";
    public const string SlaWarning = "SlaWarning";

    public static readonly IReadOnlyCollection<string> Allowed =
    [
        TicketUpdated,
        Assignment,
        Resolution,
        System,
        Message,
        SlaWarning
    ];

    public static string Normalize(string type)
    {
        var normalized = Allowed.FirstOrDefault(
            allowed => string.Equals(allowed, type.Trim(), StringComparison.OrdinalIgnoreCase));

        return normalized ?? throw new Exception(
            $"Notification type không hợp lệ. Các giá trị được phép: {string.Join(", ", Allowed)}.");
    }
}
