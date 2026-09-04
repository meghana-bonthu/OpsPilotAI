namespace OpsPilot.Api.AI;

public sealed class DisabledIncidentSemanticSearchGateway
    : IIncidentSemanticSearchGateway
{
    public Task<IReadOnlyList<Guid>?> SearchAsync(
        string query,
        IReadOnlyList<IncidentSearchDocument> incidents,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyList<Guid>?>(null);
    }
}
