using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UrbanService.BLL.Interfaces;

namespace UrbanService.Hubs;

/// <summary>
/// Kênh realtime cho hội thoại giữa người dân và nhân sự xử lý trên từng ticket.
///
/// Mỗi ticket có hai group: một group chung cho mọi người được đọc hội thoại, và
/// một group nội bộ chỉ dành cho nhân sự. Tách như vậy vì ghi chú nội bộ không bao
/// giờ được xuống máy người dân, mà nếu chỉ dùng một group thì client phải tự lọc,
/// tức là dữ liệu đã rời khỏi server rồi mới bị giấu đi.
///
/// Quyền vào group được kiểm qua <see cref="IInteractionMessageService"/> thay vì
/// truy vấn database tại đây, để luật phân quyền chỉ nằm một chỗ.
/// </summary>
[Authorize]
public class InteractionMessageHub : Hub
{
    private readonly IInteractionMessageService _messageService;

    private readonly ILogger<InteractionMessageHub> _logger;

    public InteractionMessageHub(
        IInteractionMessageService messageService,
        ILogger<InteractionMessageHub> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }

    /// <summary>Group nhận mọi tin nhắn công khai của một ticket.</summary>
    public static string TicketGroup(Guid feedbackId) => $"ticket:{feedbackId}";

    /// <summary>Group chỉ dành cho nhân sự, nhận thêm ghi chú nội bộ.</summary>
    public static string TicketInternalGroup(Guid feedbackId) => $"ticket:{feedbackId}:internal";

    /// <summary>
    /// Client xin theo dõi một ticket. Trả về true khi người gọi được nhận cả ghi
    /// chú nội bộ, để client biết có cần tải lại kèm internal hay không.
    /// </summary>
    public async Task<bool> JoinTicket(string feedbackId)
    {
        var ticketId = ParseFeedbackId(feedbackId);
        var access = await _messageService.EnsureConversationAccessAsync(
            GetCurrentUserId(),
            ticketId);

        await Groups.AddToGroupAsync(Context.ConnectionId, TicketGroup(ticketId));

        if (access.CanViewInternal)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, TicketInternalGroup(ticketId));
        }

        _logger.LogInformation(
            "SignalR connection {ConnectionId} joined ticket {FeedbackId}. Internal: {CanViewInternal}",
            Context.ConnectionId,
            ticketId,
            access.CanViewInternal);

        return access.CanViewInternal;
    }

    /// <summary>
    /// Rời ticket khi client đóng hội thoại.
    ///
    /// Không kiểm quyền ở đây: rời một group mình không ở trong là thao tác vô hại,
    /// còn bắt kiểm quyền thì một người vừa bị thu hồi quyền sẽ không thoát ra được.
    /// </summary>
    public async Task LeaveTicket(string feedbackId)
    {
        var ticketId = ParseFeedbackId(feedbackId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TicketGroup(ticketId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TicketInternalGroup(ticketId));
    }

    private static Guid ParseFeedbackId(string feedbackId)
    {
        return Guid.TryParse(feedbackId, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new HubException("feedbackId không hợp lệ.");
    }

    private Guid GetCurrentUserId()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userId, out var parsed)
            ? parsed
            : throw new HubException("Phiên đăng nhập không hợp lệ.");
    }
}
