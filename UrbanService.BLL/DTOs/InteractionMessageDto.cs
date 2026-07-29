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