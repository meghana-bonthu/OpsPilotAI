using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Contracts;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;
using Microsoft.AspNetCore.Authorization;

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
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .Select(incident => ToResponse(incident))
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
        var incident = await dbContext.Incidents
            .AsNoTracking()
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
        var incidentExists = await dbContext.Incidents
            .AsNoTracking()
            .AnyAsync(
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
                change.ChangedAtUtc))
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
        var incident = new Incident(
            request.Title,
            request.Description,
            request.Priority);

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

        IncidentStatusChange statusChange;

        try
        {
            statusChange = incident.ChangeStatus(request.Status);
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

    private static IncidentResponse ToResponse(Incident incident)
    {
        return new IncidentResponse(
            incident.Id,
            incident.Title,
            incident.Description,
            incident.Priority,
            incident.Status,
            incident.CreatedAtUtc);
    }
}