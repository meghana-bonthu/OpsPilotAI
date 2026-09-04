using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Contracts;

public sealed record IncidentSuggestedActionResponse(
    Guid Id,
    Guid IncidentId,
    string Action,
    string PromptVersion,
    SuggestedActionStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? DecidedByUserId);
