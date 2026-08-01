namespace UrbanService.BLL.Common.Constraint;

public static class SlaStatus
{
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    private static readonly string[] AllowedStatuses =
    [
        Running,
        Paused,
        Completed,
        Cancelled
    ];

    public static string Normalize(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new Exception("SLA status is required.");
        }

        var normalized = AllowedStatuses.FirstOrDefault(
            allowed => string.Equals(
                allowed,
                status.Trim(),
                StringComparison.OrdinalIgnoreCase));

        return normalized
            ?? throw new Exception(
                $"SLA status không hợp lệ. Các giá trị được phép: {string.Join(", ", AllowedStatuses)}.");
    }
}