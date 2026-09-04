namespace OpsPilot.Api.Domain;

public sealed class IncidentSuggestedAction
{
    private IncidentSuggestedAction() { }

    public IncidentSuggestedAction(
        Guid incidentId,
        string action,
        DateTimeOffset createdAtUtc)
    {
        if (incidentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Incident ID cannot be empty.",
                nameof(incidentId));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException(
                "Suggested action is required.",
                nameof(action));
        }

        Id = Guid.NewGuid();
        IncidentId = incidentId;
        Action = action.Trim();
        Status = SuggestedActionStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid IncidentId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public SuggestedActionStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public string? DecidedByUserId { get; private set; }

    public void Approve(
        string decidedByUserId,
        DateTimeOffset decidedAtUtc)
    {
        Decide(
            SuggestedActionStatus.Approved,
            decidedByUserId,
            decidedAtUtc);
    }

    public void Reject(
        string decidedByUserId,
        DateTimeOffset decidedAtUtc)
    {
        Decide(
            SuggestedActionStatus.Rejected,
            decidedByUserId,
            decidedAtUtc);
    }

    private void Decide(
        SuggestedActionStatus status,
        string decidedByUserId,
        DateTimeOffset decidedAtUtc)
    {
        if (Status != SuggestedActionStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending suggested actions can be decided.");
        }

        if (string.IsNullOrWhiteSpace(decidedByUserId))
        {
            throw new ArgumentException(
                "Deciding user ID is required.",
                nameof(decidedByUserId));
        }

        Status = status;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = decidedAtUtc;
    }
}

public enum SuggestedActionStatus
{
    Pending,
    Approved,
    Rejected
}
