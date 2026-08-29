namespace OpsPilot.Api.Domain;

public sealed class Incident
{
    private Incident() { }

    public Incident(
        string title,
        string description,
        IncidentPriority priority)
    {
        Id = Guid.NewGuid();
        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        Status = IncidentStatus.New;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public IncidentPriority Priority { get; private set; }

    public IncidentStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void ChangeStatus(IncidentStatus nextStatus)
    {
        if (!CanTransitionTo(nextStatus))
        {
            throw new InvalidOperationException(
                $"An incident cannot transition from {Status} to {nextStatus}.");
        }

        Status = nextStatus;
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