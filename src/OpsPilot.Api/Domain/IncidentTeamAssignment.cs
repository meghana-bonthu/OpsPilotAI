namespace OpsPilot.Api.Domain;

public sealed class IncidentTeamAssignment
{
    private IncidentTeamAssignment() { }

    public IncidentTeamAssignment(
        Guid incidentId,
        Guid teamId,
        DateTimeOffset assignedAtUtc,
        string assignedByUserId)
    {
        Id = Guid.NewGuid();
        IncidentId = incidentId;
        TeamId = teamId;
        AssignedAtUtc = assignedAtUtc;
        AssignedByUserId = assignedByUserId;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public Guid TeamId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }

    public string AssignedByUserId { get; private set; } = string.Empty;
}