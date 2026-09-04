namespace OpsPilot.Api.AI;

public sealed class LocalIncidentSemanticSearchGateway
    : IIncidentSemanticSearchGateway
{
    private static readonly IReadOnlyDictionary<string, string[]> Concepts =
        new Dictionary<string, string[]>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["database"] =
                ["database", "db", "sql", "storage"],
            ["outage"] =
                ["outage", "down", "unavailable", "offline"],
            ["authentication"] =
                ["authentication", "auth", "login", "signin", "sign-in"],
            ["payment"] =
                ["payment", "billing", "checkout", "transaction"],
            ["network"] =
                ["network", "connectivity", "connection", "internet"],
            ["performance"] =
                ["performance", "slow", "latency", "timeout", "delay"]
        };

    public Task<IReadOnlyList<Guid>?> SearchAsync(
        string query,
        IReadOnlyList<IncidentSearchDocument> incidents,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queryTerms = ExpandTerms(Tokenize(query));

        var rankedIds = incidents
            .Select(
                incident => new
                {
                    incident.Id,
                    Score = Score(
                        queryTerms,
                        incident.Title,
                        incident.Description)
                })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Id)
            .Select(result => result.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<Guid>?>(
            rankedIds);
    }

    private static int Score(
        HashSet<string> queryTerms,
        string title,
        string description)
    {
        var titleTerms =
            ExpandTerms(Tokenize(title));

        var descriptionTerms =
            ExpandTerms(Tokenize(description));

        var titleMatches =
            queryTerms.Count(titleTerms.Contains);

        var descriptionMatches =
            queryTerms.Count(descriptionTerms.Contains);

        return (titleMatches * 3) + descriptionMatches;
    }

    private static HashSet<string> ExpandTerms(
        IEnumerable<string> terms)
    {
        var expanded = new HashSet<string>(
            terms,
            StringComparer.OrdinalIgnoreCase);

        foreach (var concept in Concepts)
        {
            if (!concept.Value.Any(expanded.Contains))
            {
                continue;
            }

            expanded.Add(concept.Key);

            foreach (var synonym in concept.Value)
            {
                expanded.Add(synonym);
            }
        }

        return expanded;
    }

    private static IEnumerable<string> Tokenize(
        string value)
    {
        return value
            .ToLowerInvariant()
            .Split(
                [
                    ' ',
                    ',',
                    '.',
                    ':',
                    ';',
                    '/',
                    '\\',
                    '-',
                    '_',
                    '(',
                    ')'
                ],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
    }
}
