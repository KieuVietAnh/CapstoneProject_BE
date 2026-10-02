using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Common.Helpers;
using UrbanService.BLL.Dtos;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services;

public class ProviderAssignmentNotifier : IProviderAssignmentNotifier
{
    private readonly IUnitOfWork _uow;

    private readonly IEmailSender _emailSender;

    private readonly ProviderNotificationOptions _options;

    private readonly ILogger<ProviderAssignmentNotifier> _logger;

    public ProviderAssignmentNotifier(
        IUnitOfWork uow,
        IEmailSender emailSender,
        IOptions<ProviderNotificationOptions> options,
        ILogger<ProviderAssignmentNotifier> logger)
    {
        _uow = uow;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyAssignmentAsync(
        int providerReportId,
        Guid assignedByUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(providerReportId, assignedByUserId, cancellationToken);
        }
        catch (Exception exception)
        {
            /*
             * Phân công đã lưu xong trước khi gọi tới đây. Một lá email hỏng không
             * được phép làm thao tác nghiệp vụ đã thành công báo lỗi ngược cho Staff.
             */
            _logger.LogError(
                exception,
                "Không gửi được thông báo giao việc cho provider report {ProviderReportId}.",
                providerReportId);
        }
    }

    private async Task SendAsync(
        int providerReportId,
        Guid assignedByUserId,
        CancellationToken cancellationToken)
    {
        if (!_options.AssignmentEmailEnabled)
        {
            _logger.LogInformation(
                "Bỏ qua email giao việc cho provider report {ProviderReportId}: tính năng đang tắt.",
                providerReportId);
            return;
        }

        var report = await _uow.GetRepository<FeedbackProviderReport>().Entities
            .AsNoTracking()
            .Include(item => item.Coordinator)
            .Include(item => item.Incident)
                .ThenInclude(incident => incident.Area)
            .Include(item => item.Incident)
                .ThenInclude(incident => incident.Category)
            .FirstOrDefaultAsync(item => item.ProviderReportId == providerReportId, cancellationToken);

        if (report is null)
        {
            _logger.LogWarning(
                "Không tìm thấy provider report {ProviderReportId} để gửi thông báo giao việc.",
                providerReportId);
            return;
        }

        var coordinator = report.Coordinator;

        if (string.IsNullOrWhiteSpace(coordinator.Email))
        {
            /*
             * Email không bắt buộc trên hồ sơ đơn vị. Ghi lại để Staff thấy ngay
             * trong nhật ký liên hệ rằng hệ thống không báo được và họ phải gọi.
             */
            await WriteContactLogAsync(
                report,
                assignedByUserId,
                ProviderContactResult.NotSent,
                $"Hệ thống không gửi được email giao việc vì đơn vị chưa khai báo email. "
                    + $"Vui lòng liên hệ qua số {coordinator.PhoneNumber} và ghi nhận lại.",
                cancellationToken);

            _logger.LogWarning(
                "Coordinator {CoordinatorId} chưa có email nên không gửi được thông báo giao việc.",
                coordinator.CoordinatorId);
            return;
        }

        var resolutionDueAt = await _uow.GetRepository<IncidentSla>().Entities
            .AsNoTracking()
            .Where(sla => sla.IncidentId == report.IncidentId && sla.IsCurrent)
            .Select(sla => (DateTime?)sla.ResolutionDueAt)
            .FirstOrDefaultAsync(cancellationToken);

        var assignedBy = await _uow.GetRepository<User>().Entities
            .AsNoTracking()
            .Where(user => user.UserId == assignedByUserId)
            .Select(user => new { user.FullName, user.PhoneNumber })
            .FirstOrDefaultAsync(cancellationToken);

        var htmlBody = BuildAssignmentEmailHtml(
            coordinatorName: WebUtility.HtmlEncode(coordinator.CoordinatorName),
            providerName: WebUtility.HtmlEncode(coordinator.ProviderName),
            incidentCode: BuildIncidentCode(report.IncidentId),
            incidentTitle: WebUtility.HtmlEncode(report.Incident.Title),
            description: WebUtility.HtmlEncode(
                string.IsNullOrWhiteSpace(report.Incident.Description)
                    ? "Không có mô tả chi tiết."
                    : report.Incident.Description),
            locationText: WebUtility.HtmlEncode(report.Incident.LocationText),
            areaName: WebUtility.HtmlEncode(report.Incident.Area?.AreaName ?? "Chưa xác định"),
            categoryName: WebUtility.HtmlEncode(
                report.Incident.Category?.CategoryName ?? "Chưa phân loại"),
            priority: WebUtility.HtmlEncode(report.Incident.Priority ?? "Chưa xác định"),
            deadlineDisplay: resolutionDueAt.HasValue
                ? SlaDateTimeHelper.FormatVietnamDateTime(resolutionDueAt.Value)
                : "Chưa thiết lập SLA",
            assignedByName: WebUtility.HtmlEncode(assignedBy?.FullName ?? "Nhân sự UrbanService"),
            assignedByPhone: WebUtility.HtmlEncode(assignedBy?.PhoneNumber ?? "Chưa có"));

        try
        {
            await _emailSender.SendAsync(
                new EmailMessageDto
                {
                    To = [coordinator.Email.Trim()],
                    Subject = "[UrbanService] Đơn vị của Quý vị vừa được giao một sự vụ",
                    Body = htmlBody,
                    IsHtml = true
                },
                cancellationToken);

            await WriteContactLogAsync(
                report,
                assignedByUserId,
                ProviderContactResult.Sent,
                $"Hệ thống tự động gửi email giao việc tới {coordinator.Email.Trim()} "
                    + "khi Staff phân công đơn vị xử lý.",
                cancellationToken);

            _logger.LogInformation(
                "Đã gửi email giao việc tới coordinator {CoordinatorId} cho incident {IncidentId}.",
                coordinator.CoordinatorId,
                report.IncidentId);
        }
        catch (Exception exception)
        {
            await WriteContactLogAsync(
                report,
                assignedByUserId,
                ProviderContactResult.Failed,
                $"Hệ thống gửi email giao việc tới {coordinator.Email.Trim()} nhưng thất bại. "
                    + $"Vui lòng liên hệ qua số {coordinator.PhoneNumber} và ghi nhận lại.",
                cancellationToken);

            _logger.LogError(
                exception,
                "Gửi email giao việc tới coordinator {CoordinatorId} thất bại cho incident {IncidentId}.",
                coordinator.CoordinatorId,
                report.IncidentId);
        }
    }

    /*
     * Ghi vào đúng bảng nhật ký liên hệ mà Staff vẫn dùng, thay vì tạo chỗ lưu riêng.
     * Nhờ vậy việc hệ thống đã báo hay chưa hiện ngay trong màn hình Staff đang mở,
     * cùng một dòng thời gian với những lần gọi điện thủ công.
     */
    private async Task WriteContactLogAsync(
        FeedbackProviderReport report,
        Guid assignedByUserId,
        string contactResult,
        string note,
        CancellationToken cancellationToken)
    {
        await _uow.GetRepository<ProviderContactLog>().AddAsync(new ProviderContactLog
        {
            ProviderReportId = report.ProviderReportId,
            CoordinatorId = report.CoordinatorId,
            ContactedByUserId = assignedByUserId,
            ContactMethod = ProviderContactMethod.Email,
            ContactResult = contactResult,
            ContactNote = note,
            ContactedAt = DateTime.UtcNow
        });

        await _uow.SaveAsync();
        _ = cancellationToken;
    }

    /// <summary>Mã hiển thị của sự vụ, trùng với mã mà giao diện đang cho người dùng thấy.</summary>
    private static string BuildIncidentCode(Guid incidentId)
    {
        var compact = incidentId.ToString("N");
        return $"UM-{compact[..8].ToUpperInvariant()}";
    }

    private static string BuildAssignmentEmailHtml(
        string coordinatorName,
        string providerName,
        string incidentCode,
        string incidentTitle,
        string description,
        string locationText,
        string areaName,
        string categoryName,
        string priority,
        string deadlineDisplay,
        string assignedByName,
        string assignedByPhone)
    {
        var labelCellStyle =
            "width:34%;" +
            "padding:11px 12px;" +
            "background:#f9fafb;" +
            "border:1px solid #e5e7eb;" +
            "font-weight:600;" +
            "vertical-align:top;";

        var valueCellStyle =
            "padding:11px 12px;" +
            "border:1px solid #e5e7eb;" +
            "vertical-align:top;";

        return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport"
          content="width=device-width, initial-scale=1.0">
</head>
<body style="
    margin:0;
    padding:0;
    background-color:#f4f6f8;
    font-family:Arial, Helvetica, sans-serif;
    color:#1f2937;">

    <table width="100%"
           cellpadding="0"
           cellspacing="0"
           role="presentation"
           style="background-color:#f4f6f8;
                  padding:24px 12px;">
        <tr>
            <td align="center">
                <table width="640"
                       cellpadding="0"
                       cellspacing="0"
                       role="presentation"
                       style="
                           width:100%;
                           max-width:640px;
                           background:#ffffff;
                           border-radius:12px;
                           overflow:hidden;
                           box-shadow:0 4px 16px rgba(0,0,0,0.08);">

                    <tr>
                        <td style="
                            background:#2563eb;
                            color:#ffffff;
                            padding:22px 28px;">
                            <div style="
                                font-size:22px;
                                font-weight:700;">
                                UrbanService
                            </div>
                            <div style="
                                margin-top:6px;
                                font-size:15px;">
                                Thông báo giao việc xử lý
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td style="padding:28px;">
                            <p style="
                                margin:0 0 16px;
                                font-size:16px;">
                                Kính gửi
                                <strong>{{coordinatorName}}</strong>,
                            </p>

                            <p style="
                                margin:0 0 20px;
                                line-height:1.6;
                                font-size:15px;">
                                Hệ thống UrbanService vừa giao một sự vụ cho
                                <strong>{{providerName}}</strong>.
                                Thời hạn xử lý đã bắt đầu được tính từ thời điểm
                                phân công.
                            </p>

                            <div style="
                                background:#eff6ff;
                                border-left:5px solid #2563eb;
                                padding:16px 18px;
                                margin-bottom:22px;
                                border-radius:6px;">
                                <strong style="color:#1d4ed8;">
                                    Hạn hoàn thành:
                                </strong>
                                {{deadlineDisplay}}
                            </div>

                            <table width="100%"
                                   cellpadding="0"
                                   cellspacing="0"
                                   role="presentation"
                                   style="
                                       border-collapse:collapse;
                                       font-size:14px;
                                       margin-bottom:24px;">
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Mã sự vụ
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        <strong>{{incidentCode}}</strong>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Tiêu đề
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{incidentTitle}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Danh mục
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{categoryName}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Khu vực
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{areaName}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Địa điểm
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{locationText}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Mức ưu tiên
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{priority}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Mô tả
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{description}}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="{{labelCellStyle}}">
                                        Đầu mối liên hệ
                                    </td>
                                    <td style="{{valueCellStyle}}">
                                        {{assignedByName}} &middot; {{assignedByPhone}}
                                    </td>
                                </tr>
                            </table>

                            <p style="
                                margin:0 0 16px;
                                line-height:1.6;
                                font-size:15px;">
                                Đề nghị Quý đơn vị xác nhận tiếp nhận với đầu mối
                                nêu trên, triển khai xử lý và cung cấp hình ảnh
                                hiện trường sau khi hoàn thành để làm căn cứ
                                nghiệm thu.
                            </p>

                            <p style="
                                margin:24px 0 0;
                                line-height:1.6;
                                font-size:15px;">
                                Trân trọng,<br>
                                <strong>UrbanService System</strong>
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style="
                            background:#f9fafb;
                            padding:16px 28px;
                            color:#6b7280;
                            font-size:12px;
                            line-height:1.5;">
                            Đây là email tự động từ hệ thống UrbanService.
                            Vui lòng không trả lời trực tiếp email này.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
""";
    }
}
