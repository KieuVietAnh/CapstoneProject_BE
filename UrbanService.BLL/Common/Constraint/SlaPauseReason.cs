namespace UrbanService.BLL.Common.Constraint;

public static class SlaPauseReason
{
    public const string WaitingCitizen = "WaitingCitizen";
    public const string ForceMajeure = "ForceMajeure";
    public const string ExternalDependency = "ExternalDependency";
    public const string SystemMaintenance = "SystemMaintenance";
    public const string Other = "Other";

    public static readonly IReadOnlyCollection<string> All =
    [
        WaitingCitizen,
        ForceMajeure,
        ExternalDependency,
        SystemMaintenance,
        Other
    ];

    private static readonly string[] AllowedReasons =
    [
        WaitingCitizen,
        ForceMajeure,
        ExternalDependency,
        SystemMaintenance,
        Other
    ];

    public static string Normalize(string reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new Exception("Pause reason code is required.");
        }

        var normalized = AllowedReasons.FirstOrDefault(
            allowed => string.Equals(
                allowed,
                reasonCode.Trim(),
                StringComparison.OrdinalIgnoreCase));

        return normalized
            ?? throw new Exception(
                $"Pause reason không hợp lệ. Các giá trị được phép: {string.Join(", ", AllowedReasons)}.");
    }
}