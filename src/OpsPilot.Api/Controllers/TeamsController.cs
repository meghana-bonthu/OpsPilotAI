using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Contracts;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Route("api/teams")]
[Authorize(Policy = "AdministratorOnly")]
public sealed class TeamsController(
    OpsPilotDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TeamResponse>>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var teams = await dbContext.Teams
            .AsNoTracking()
            .OrderBy(team => team.Name)
            .Select(team => new TeamResponse(
                team.Id,
                team.Name))
            .ToListAsync(cancellationToken);

        return Ok(teams);
    }

    [HttpPost]
    [ProducesResponseType<TeamResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamResponse>> Create(
        CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();

        var teamExists = await dbContext.Teams
            .AsNoTracking()
            .AnyAsync(
                team => team.Name == normalizedName,
                cancellationToken);

        if (teamExists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Team already exists",
                Detail =
                    "A team with the same name already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var team = new Team(normalizedName);

        dbContext.Teams.Add(team);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new TeamResponse(
            team.Id,
            team.Name);

        return Created(
            $"/api/teams/{team.Id}",
            response);
    }
}