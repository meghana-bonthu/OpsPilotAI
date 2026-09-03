namespace OpsPilot.Api.Contracts;

public sealed record IncidentSummaryResponse(
    Guid IncidentId,
    string Summary);
