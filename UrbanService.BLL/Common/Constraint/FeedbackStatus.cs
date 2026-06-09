namespace UrbanService.BLL.Common.Constraint;

public static class FeedbackStatus
{
    public const string Submitted = "Submitted";
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

    public static readonly IReadOnlyCollection<string> Allowed =
    [
        Submitted,
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

    public static string Normalize(string status)
    {
        var normalized = Allowed.FirstOrDefault(
            allowed => string.Equals(allowed, status.Trim(), StringComparison.OrdinalIgnoreCase));

        return normalized ?? throw new Exception(
            $"Status không hợp lệ. Các giá trị được phép: {string.Join(", ", Allowed)}.");
    }
}
