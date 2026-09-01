using System.ComponentModel.DataAnnotations;

namespace OpsPilot.Api.Contracts;

public sealed record CreateTeamRequest(
    [param: Required, StringLength(120, MinimumLength = 2)]
    string Name);

public sealed record TeamResponse(
    Guid Id,
    string Name);