namespace OpsPilot.Api.AI;

public sealed class LocalIncidentSummaryGateway
    : IIncidentSummaryGateway
{
    public Task<string> GenerateSummaryAsync(
        string title,
        string description,
        string priority,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summary =
            $"{title}: {description} Priority: {priority}.";

        return Task.FromResult(summary);
    }
}
