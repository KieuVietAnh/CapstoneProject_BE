namespace UrbanService.DAL.Entities;

public sealed class IncidentSupport
{
    public Guid IncidentSupportId { get; set; }

    public Guid IncidentId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Incident Incident { get; set; } = null!;

    public User User { get; set; } = null!;
}
