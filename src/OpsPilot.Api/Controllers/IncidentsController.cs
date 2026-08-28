using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Contracts;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController(OpsPilotDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IncidentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IncidentResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new IncidentResponse(x.Id, x.Title, x.Description, x.Priority, x.Status, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(incidents);
    }

    [HttpPost]
    [ProducesResponseType<IncidentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentResponse>> Create(
        CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var incident = new Incident(request.Title, request.Description, request.Priority);
        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new IncidentResponse(
            incident.Id, incident.Title, incident.Description, incident.Priority, incident.Status, incident.CreatedAtUtc);

        return Created($"/api/incidents/{incident.Id}", response);
    }
}
