namespace UrbanService.BLL.Common.Constraint;

public static class SlaTargetStatus
{
    public const string Pending = "Pending";
    public const string Met = "Met";
    public const string Breached = "Breached";

    private static readonly string[] AllowedStatuses =
    [
        Pending,
        Met,
        Breached
    ];

    public static string Normalize(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new Exception("SLA target status is required.");
        }

        var normalized = AllowedStatuses.FirstOrDefault(
            allowed => string.Equals(
                allowed,
                status.Trim(),
                StringComparison.OrdinalIgnoreCase));

        return normalized
            ?? throw new Exception(
                $"SLA target status không hợp lệ. Các giá trị được phép: {string.Join(", ", AllowedStatuses)}.");
    }
}