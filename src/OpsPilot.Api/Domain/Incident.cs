
namespace OpsPilot.Api.Domain;

public sealed class Incident
{
    private readonly List<IncidentStatusChange> _statusHistory = [];
    public IReadOnlyCollection<IncidentTeamAssignment> TeamAssignmentHistory =>
    _teamAssignmentHistory.AsReadOnly();
    private readonly List<IncidentTeamAssignment> _teamAssignmentHistory = [];
    private Incident() { }

    public Incident(
    string title,
    string description,
    IncidentPriority priority,
    string reporterUserId)
    {
        Id = Guid.NewGuid();
        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        ReporterUserId = reporterUserId;
        Status = IncidentStatus.New;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public IncidentPriority Priority { get; private set; }
    public string ReporterUserId { get; private set; } = string.Empty;
    public Guid? TeamId { get; private set; }
    public IncidentStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<IncidentStatusChange> StatusHistory =>
        _statusHistory.AsReadOnly();

    public IncidentStatusChange ChangeStatus(
    IncidentStatus nextStatus,
    string changedByUserId)
    {
        if (!CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException(
                $"An incident cannot transition from {Status} to {nextStatus}.");
        }

        var previousStatus = Status;
        Status = nextStatus;

        var statusChange = new IncidentStatusChange(
            Id,
            previousStatus,
            nextStatus,
            DateTimeOffset.UtcNow,
            changedByUserId);

        _statusHistory.Add(statusChange);

        return statusChange;
    }
    public IncidentTeamAssignment AssignTeam(
    Guid teamId,
    string assignedByUserId)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException(
                "Team ID cannot be empty.",
                nameof(teamId));
        }

        if (string.IsNullOrWhiteSpace(assignedByUserId))
        {
            throw new ArgumentException(
                "Assigned-by user ID is required.",
                nameof(assignedByUserId));
        }

        TeamId = teamId;

        var assignment = new IncidentTeamAssignment(
            Id,
            teamId,
            DateTimeOffset.UtcNow,
            assignedByUserId);

        _teamAssignmentHistory.Add(assignment);

        return assignment;
    }
    public bool CanTransitionTo(IncidentStatus nextStatus)
    {
        return Status switch
        {
            IncidentStatus.New =>
                nextStatus is IncidentStatus.Triaged
                    or IncidentStatus.Cancelled,

            IncidentStatus.Triaged =>
                nextStatus is IncidentStatus.InProgress
                    or IncidentStatus.Cancelled,

            IncidentStatus.InProgress =>
                nextStatus is IncidentStatus.Resolved
                    or IncidentStatus.Cancelled,

            IncidentStatus.Resolved =>
                nextStatus is IncidentStatus.Closed
                    or IncidentStatus.InProgress,

            IncidentStatus.Closed => false,

            IncidentStatus.Cancelled => false,

            _ => false
        };
    }
}

public enum IncidentPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum IncidentStatus
{
    New,
    Triaged,
    InProgress,
    Resolved,
    Closed,
    Cancelled
}