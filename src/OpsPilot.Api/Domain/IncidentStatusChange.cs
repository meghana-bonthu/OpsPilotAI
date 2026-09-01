namespace OpsPilot.Api.Domain;

public sealed class IncidentStatusChange
{
    private IncidentStatusChange() { }

    internal IncidentStatusChange(
    Guid incidentId,
    IncidentStatus previousStatus,
    IncidentStatus newStatus,
    DateTimeOffset changedAtUtc,
    string changedByUserId)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedAtUtc = changedAtUtc;
        ChangedByUserId = changedByUserId;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public IncidentStatus PreviousStatus { get; private set; }

    public IncidentStatus NewStatus { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public string ChangedByUserId { get; private set; } = string.Empty;
}