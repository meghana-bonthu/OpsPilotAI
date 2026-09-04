namespace OpsPilot.Api.AI;

public interface IIncidentSemanticSearchGateway
{
    Task<IReadOnlyList<Guid>?> SearchAsync(
        string query,
        IReadOnlyList<IncidentSearchDocument> incidents,
        CancellationToken cancellationToken);
}

public sealed record IncidentSearchDocument(
    Guid Id,
    string Title,
    string Description);
