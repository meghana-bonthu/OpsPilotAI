using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Contracts;

public sealed record IncidentSuggestedActionResponse(
    Guid Id,
    Guid IncidentId,
    string Action,
    SuggestedActionStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? DecidedByUserId);
