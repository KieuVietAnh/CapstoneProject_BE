namespace UrbanService.BLL.Options;

public sealed class SlaMonitoringOptions
{
    public const string SectionName = "SlaMonitoring";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Chu kỳ worker chạy, tính bằng phút.
    /// </summary>
    public int IntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Delay trước vòng kiểm tra đầu tiên khi application start.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 10;

    /// <summary>
    /// Khi SLA còn bao nhiêu phần trăm thời gian thì phát cảnh báo.
    /// Ví dụ 30 nghĩa là cảnh báo khi còn <= 30%.
    /// </summary>
    public int WarningThresholdPercent { get; set; } = 30;
}