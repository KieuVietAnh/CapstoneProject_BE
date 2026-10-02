namespace UrbanService.BLL.DTOs;

public class InteractionMessageDto
{
    public int InteractionMessageId { get; set; }

    public Guid FeedbackId { get; set; }

    public Guid UserId { get; set; }

    public string? UserFullName { get; set; }

    public string? UserEmail { get; set; }

    public string? UserRole { get; set; }

    public string SenderType { get; set; } = null!;

    public string MessageText { get; set; } = null!;

    public bool IsInternal { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class InteractionMessageCreateRequest
{
    public string MessageText { get; set; } = null!;

    public bool IsInternal { get; set; }
}

public class SystemInteractionMessageCreateRequest
{
    public string MessageText { get; set; } = null!;

    public bool IsInternal { get; set; } = true;
}

/// <summary>
/// Quyền của một người dùng với hội thoại của ticket.
///
/// Dùng khi client xin tham gia kênh realtime: hub cần biết họ có được đọc hội
/// thoại không, và nếu có thì có được nhận ghi chú nội bộ không, trước khi cho vào
/// group. Không có lớp này thì hub phải tự truy vấn database và lặp lại luật phân
/// quyền vốn đã nằm trong business service.
/// </summary>
public class InteractionConversationAccessDto
{
    public Guid FeedbackId { get; set; }

    /// <summary>Người dân chủ phản ánh; họ không bao giờ thấy ghi chú nội bộ.</summary>
    public bool IsResidentOwner { get; set; }

    /// <summary>Staff, Manager hoặc Admin trong phạm vi phụ trách.</summary>
    public bool CanViewInternal { get; set; }
}
