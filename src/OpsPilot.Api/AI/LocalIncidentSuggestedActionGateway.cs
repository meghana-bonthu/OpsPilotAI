namespace OpsPilot.Api.AI;

public sealed class LocalIncidentSuggestedActionGateway
    : IIncidentSuggestedActionGateway
{
    public Task<string> GenerateSuggestedActionAsync(
        string title,
        string description,
        string priority,
        string status,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var suggestedAction =
            $"Review the {priority.ToLowerInvariant()} priority incident " +
            $"'{title}' and determine the next appropriate response " +
            $"for its current {status} status.";

        return Task.FromResult(suggestedAction);
    }
}
