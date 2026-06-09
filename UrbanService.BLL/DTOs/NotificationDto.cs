namespace UrbanService.BLL.Dtos;

public class NotificationDto
{
    public int NotificationId { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Type { get; set; } = null!;

    public bool IsRead { get; set; }

    public string? TargetUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
