namespace UrbanService.BLL.Dtos;

public class AreaAlertQueryParameters
{
    public bool OnlySubscribedAreas { get; set; }

    public int? AreaId { get; set; }

    public string? Status { get; set; }

    public string? Severity { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public class CreateAreaAlertRequest
{
    public int AreaId { get; set; }

    public int? CategoryId { get; set; }

    public int? HotspotId { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string AlertType { get; set; } = "Manual";

    public string Severity { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? RadiusMeters { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }
}

public class CreateAreaAlertFromFeedbackRequest
{
    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public int? RadiusMeters { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }
}

public class UserAreaAlertDto
{
    public int AlertId { get; set; }

    public int AreaId { get; set; }

    public string? AreaName { get; set; }

    public Guid CreatedByUserId { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int? HotspotId { get; set; }

    public string? HotspotName { get; set; }

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string AlertType { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? RadiusMeters { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsSubscribedArea { get; set; }

    public Guid? SourceFeedbackId { get; set; }
}

public class CreateAreaSubscriptionRequest
{
    public int AreaId { get; set; }

    public bool IsPrimaryArea { get; set; }

    public bool ReceiveAlerts { get; set; } = true;
}

public class UserAreaSubscriptionDto
{
    public int SubscriptionId { get; set; }

    public Guid UserId { get; set; }

    public int AreaId { get; set; }

    public string? AreaName { get; set; }

    public bool IsPrimaryArea { get; set; }

    public bool ReceiveAlerts { get; set; }

    public DateTime CreatedAt { get; set; }
}