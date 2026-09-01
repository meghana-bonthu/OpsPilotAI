using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Contracts;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;
using System.Security.Claims;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize]
public sealed class IncidentsController(
    OpsPilotDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IncidentResponse>>(
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncidentResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var query = dbContext.Incidents
            .AsNoTracking()
            .AsQueryable();

        var hasElevatedAccess =
            User.IsInRole("Responder") ||
            User.IsInRole("Administrator");

        if (!hasElevatedAccess)
        {
            var reporterUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(reporterUserId))
            {
                return Unauthorized();
            }

            query = query.Where(
                incident =>
                    incident.ReporterUserId == reporterUserId);
        }

        var incidents = await query
            .OrderByDescending(
                incident => incident.CreatedAtUtc)
            .Select(
                incident => ToResponse(incident))
            .ToListAsync(cancellationToken);

        return Ok(incidents);
    }

    [HttpGet("{id:guid}", Name = "GetIncidentById")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Incidents
            .AsNoTracking()
            .AsQueryable();

        var hasElevatedAccess =
            User.IsInRole("Responder") ||
            User.IsInRole("Administrator");

        if (!hasElevatedAccess)
        {
            var reporterUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(reporterUserId))
            {
                return Unauthorized();
            }

            query = query.Where(
                current =>
                    current.ReporterUserId == reporterUserId);
        }

        var incident = await query
            .SingleOrDefaultAsync(
                current => current.Id == id,
                cancellationToken);

        return incident is null
            ? NotFound()
            : Ok(ToResponse(incident));
    }

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<IReadOnlyList<IncidentStatusChangeResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<IncidentStatusChangeResponse>>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incidentQuery = dbContext.Incidents
            .AsNoTracking()
            .AsQueryable();

        var hasElevatedAccess =
            User.IsInRole("Responder") ||
            User.IsInRole("Administrator");

        if (!hasElevatedAccess)
        {
            var reporterUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(reporterUserId))
            {
                return Unauthorized();
            }

            incidentQuery = incidentQuery.Where(
                incident =>
                    incident.ReporterUserId == reporterUserId);
        }

        var incidentExists = await incidentQuery.AnyAsync(
            incident => incident.Id == id,
            cancellationToken);

        if (!incidentExists)
        {
            return NotFound();
        }

        var history = await dbContext.IncidentStatusChanges
            .AsNoTracking()
            .Where(change => change.IncidentId == id)
            .OrderByDescending(change => change.ChangedAtUtc)
            .Select(change => new IncidentStatusChangeResponse(
                change.Id,
                change.PreviousStatus,
                change.NewStatus,
                change.ChangedAtUtc,
                change.ChangedByUserId))
            .ToListAsync(cancellationToken);

        return Ok(history);
    }

    [Authorize(Policy = "ReporterOnly")]
    [HttpPost]
    [ProducesResponseType<IncidentResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentResponse>> Create(
        CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var reporterUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(reporterUserId))
        {
            return Unauthorized();
        }

        var incident = new Incident(
            request.Title,
            request.Description,
            request.Priority,
            reporterUserId);

        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(incident);

        return CreatedAtRoute(
            "GetIncidentById",
            new { id = incident.Id },
            response);
    }

    [Authorize(Policy = "ResponderOrAdministrator")]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IncidentResponse>> UpdateStatus(
        Guid id,
        UpdateIncidentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(
                current => current.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return NotFound();
        }

        var changedByUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(changedByUserId))
        {
            return Unauthorized();
        }

        IncidentStatusChange statusChange;

        try
        {
            statusChange = incident.ChangeStatus(
                request.Status,
                changedByUserId);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Invalid incident status transition",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }

        dbContext.IncidentStatusChanges.Add(statusChange);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(incident));
    }

    [Authorize(Policy = "ResponderOrAdministrator")]
    [HttpPatch("{id:guid}/team")]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentResponse>> AssignTeam(
        Guid id,
        AssignIncidentTeamRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TeamId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(request.TeamId),
                "Team ID cannot be empty.");

            return ValidationProblem(ModelState);
        }

        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(
                current => current.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return NotFound();
        }

        var teamExists = await dbContext.Teams
            .AsNoTracking()
            .AnyAsync(
                team => team.Id == request.TeamId,
                cancellationToken);

        if (!teamExists)
        {
            return NotFound();
        }

        incident.AssignTeam(request.TeamId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(incident));
    }

    private static IncidentResponse ToResponse(Incident incident)
    {
        return new IncidentResponse(
    incident.Id,
    incident.Title,
    incident.Description,
    incident.Priority,
    incident.Status,
    incident.CreatedAtUtc,
    incident.TeamId);
    }
}