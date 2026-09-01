using System.ComponentModel.DataAnnotations;
using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Contracts;

public sealed record CreateIncidentRequest(
    [param: Required, StringLength(160, MinimumLength = 5)] string Title,
    [param: Required, StringLength(4000, MinimumLength = 10)] string Description,
    IncidentPriority Priority);

public sealed record IncidentResponse(
    Guid Id,
    string Title,
    string Description,
    IncidentPriority Priority,
    IncidentStatus Status,
    DateTimeOffset CreatedAtUtc);
public sealed record UpdateIncidentStatusRequest(
    IncidentStatus Status);
public sealed record IncidentStatusChangeResponse(
    Guid Id,
    IncidentStatus PreviousStatus,
    IncidentStatus NewStatus,
    DateTimeOffset ChangedAtUtc,
    string ChangedByUserId);