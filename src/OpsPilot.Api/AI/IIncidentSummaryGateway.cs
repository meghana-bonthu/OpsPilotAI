namespace OpsPilot.Api.AI;

public interface IIncidentSummaryGateway
{
    Task<string> GenerateSummaryAsync(
        string title,
        string description,
        string priority,
        CancellationToken cancellationToken);
}
