namespace OpsPilot.Api.AI;

public interface IIncidentSuggestedActionGateway
{
    Task<string> GenerateSuggestedActionAsync(
        string title,
        string description,
        string priority,
        string status,
        CancellationToken cancellationToken);
}
