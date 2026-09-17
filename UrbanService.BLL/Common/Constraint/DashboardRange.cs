namespace UrbanService.BLL.Common.Constraint;

/// <summary>
/// Các khoảng thời gian dựng sẵn cho dashboard.
///
/// Mốc bắt đầu của mỗi khoảng luôn tính theo 00:00 giờ Việt Nam, không theo UTC.
/// </summary>
public static class DashboardRange
{
    /// <summary>Không giới hạn thời gian.</summary>
    public const string All = "all";

    /// <summary>7 ngày gần nhất, tính cả hôm nay.</summary>
    public const string Last7Days = "7d";

    /// <summary>1 tháng gần nhất.</summary>
    public const string Last1Month = "1m";

    /// <summary>6 tháng gần nhất.</summary>
    public const string Last6Months = "6m";

    /// <summary>1 năm gần nhất.</summary>
    public const string Last1Year = "1y";
}
