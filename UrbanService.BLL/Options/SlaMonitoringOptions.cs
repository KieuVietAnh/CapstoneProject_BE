namespace UrbanService.BLL.Options;

public class SlaMonitoringOptions
{
    public const string SectionName = "SlaMonitoring";


    public bool Enabled { get; set; } = true;


    /// <summary>
    /// Chu kỳ worker chạy.
    /// </summary>
    public int IntervalMinutes { get; set; } = 5;


    /// <summary>
    /// Delay lúc service start.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 10;


    /// <summary>
    /// Khi còn bao nhiêu % thời gian SLA thì cảnh báo.
    /// Ví dụ 30 nghĩa là còn 30%.
    /// </summary>
    public int WarningThresholdPercent { get; set; } = 30;
}