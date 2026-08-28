namespace OpsPilot.Api.Domain;

public sealed class Incident
{
    private Incident() { }

    public Incident(string title, string description, IncidentPriority priority)
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
}

public enum IncidentPriority { Low, Medium, High, Critical }
public enum IncidentStatus { New, Triaged, InProgress, Resolved, Closed, Cancelled }
