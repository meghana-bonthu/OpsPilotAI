using OpsPilot.Api.AI;

namespace OpsPilot.Api.Tests;

public sealed class LocalIncidentSemanticSearchGatewayTests
{
    private readonly LocalIncidentSemanticSearchGateway _gateway = new();

    [Fact]
    public async Task SearchAsync_MatchesRelatedConceptsWithoutLiteralMatch()
    {
        var matchingId = Guid.NewGuid();
        var unrelatedId = Guid.NewGuid();

        var incidents = new[]
        {
            new IncidentSearchDocument(
                matchingId,
                "SQL server down",
                "Primary storage service is offline."),
            new IncidentSearchDocument(
                unrelatedId,
                "Printer issue",
                "Office printer needs paper.")
        };

        var results = await _gateway.SearchAsync(
            "database unavailable",
            incidents,
            CancellationToken.None);

        Assert.NotNull(results);
        Assert.Contains(matchingId, results);
        Assert.DoesNotContain(unrelatedId, results);
    }

    [Fact]
    public async Task SearchAsync_RanksTitleMatchAboveDescriptionMatch()
    {
        var titleMatchId = Guid.NewGuid();
        var descriptionMatchId = Guid.NewGuid();

        var incidents = new[]
        {
            new IncidentSearchDocument(
                descriptionMatchId,
                "Application issue",
                "Users report slow performance."),
            new IncidentSearchDocument(
                titleMatchId,
                "Slow application",
                "Users report an issue.")
        };

        var results = await _gateway.SearchAsync(
            "slow",
            incidents,
            CancellationToken.None);

        Assert.NotNull(results);
        Assert.Equal(
            new[] { titleMatchId, descriptionMatchId },
            results);
    }

    [Fact]
    public async Task SearchAsync_WithUnrelatedQuery_ReturnsEmptyResults()
    {
        var incidents = new[]
        {
            new IncidentSearchDocument(
                Guid.NewGuid(),
                "Database outage",
                "SQL server is offline.")
        };

        var results = await _gateway.SearchAsync(
            "printer",
            incidents,
            CancellationToken.None);

        Assert.NotNull(results);
        Assert.Empty(results);
    }
}
