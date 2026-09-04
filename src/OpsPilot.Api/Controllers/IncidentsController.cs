using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Contracts;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OpsPilot.Api.AI;

namespace OpsPilot.Api.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize]
public sealed class IncidentsController(
    OpsPilotDbContext dbContext,
    IDistributedCache cache,
    IIncidentSummaryGateway incidentSummaryGateway,
    IIncidentSuggestedActionGateway incidentSuggestedActionGateway,
    IIncidentSemanticSearchGateway incidentSemanticSearchGateway,
    ISensitiveDataRedactor sensitiveDataRedactor) : ControllerBase
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
        string cacheKey;

        if (hasElevatedAccess)
        {
            cacheKey = "incidents:all:elevated";
        }
        else
        {
            var reporterUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(reporterUserId))
            {
                return Unauthorized();
            }

            cacheKey = $"incidents:reporter:{reporterUserId}";

            query = query.Where(
                incident =>
                incident.ReporterUserId == reporterUserId);
        }

        var cachedIncidents =
            await cache.GetStringAsync(
            cacheKey,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(cachedIncidents))
        {
            var cachedResponse =
            JsonSerializer.Deserialize<List<IncidentResponse>>(
                cachedIncidents);

            if (cachedResponse is not null)
            {
                return Ok(cachedResponse);
            }
        }

        var incidents = await query
            .OrderByDescending(
                incident => incident.CreatedAtUtc)
            .Select(
                incident => ToResponse(incident))
            .ToListAsync(cancellationToken);

        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(incidents),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                TimeSpan.FromMinutes(5)
            },
            cancellationToken);
        return Ok(incidents);
    }

    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<IncidentResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<IncidentResponse>>> Search(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Search query is required",
                Detail = "Provide a non-empty search query.",
                Status = StatusCodes.Status400BadRequest
            });
        }

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

        var accessibleIncidents = await incidentQuery
            .Select(
                incident => new IncidentSearchDocument(
                    incident.Id,
                    incident.Title,
                    incident.Description))
            .ToListAsync(cancellationToken);

        var semanticDocuments =
            accessibleIncidents
                .Select(
                    incident => new IncidentSearchDocument(
                        incident.Id,
                        sensitiveDataRedactor.Redact(
                            incident.Title),
                        sensitiveDataRedactor.Redact(
                            incident.Description)))
                .ToList();

        var semanticIds =
            await incidentSemanticSearchGateway.SearchAsync(
                query.Trim(),
                semanticDocuments,
                cancellationToken);

        if (semanticIds is not null)
        {
            Response.Headers["X-OpsPilot-Search-Mode"] =
                "semantic";
            if (semanticIds.Count == 0)
            {
                return Ok(
                    Array.Empty<IncidentResponse>());
            }

            var semanticMatches = await incidentQuery
                .Where(
                    incident =>
                        semanticIds.Contains(incident.Id))
                .Select(
                    incident => ToResponse(incident))
                .ToListAsync(cancellationToken);

            var responsesById =
                semanticMatches.ToDictionary(
                    incident => incident.Id);

            var rankedResults =
                semanticIds
                    .Where(responsesById.ContainsKey)
                    .Select(
                        id => responsesById[id])
                    .ToList();

            return Ok(rankedResults);
        }

        Response.Headers["X-OpsPilot-Search-Mode"] =
            "fallback";

        var searchTerm = query.Trim();

        var incidents = await incidentQuery
            .Where(
                incident =>
                    incident.Title.Contains(searchTerm) ||
                    incident.Description.Contains(searchTerm))
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

    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType<IncidentSummaryResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentSummaryResponse>> GetSummary(
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

        if (incident is null)
        {
            return NotFound();
        }

        var redactedTitle =
            sensitiveDataRedactor.Redact(
                incident.Title);

        var redactedDescription =
            sensitiveDataRedactor.Redact(
                incident.Description);

        var summary =
            await incidentSummaryGateway.GenerateSummaryAsync(
                redactedTitle,
                redactedDescription,
                incident.Priority.ToString(),
                cancellationToken);

        return Ok(
            new IncidentSummaryResponse(
                incident.Id,
                summary));
    }
    [Authorize(Policy = "ResponderOrAdministrator")]
    [HttpGet("{id:guid}/suggested-actions")]
    [ProducesResponseType<IReadOnlyList<IncidentSuggestedActionResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<IncidentSuggestedActionResponse>>>
        GetSuggestedActions(
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

        var suggestedActions =
            await dbContext.IncidentSuggestedActions
                .AsNoTracking()
                .Where(
                    suggestedAction =>
                        suggestedAction.IncidentId == id)
                .OrderByDescending(
                    suggestedAction =>
                        suggestedAction.CreatedAtUtc)
                .Select(
                    suggestedAction =>
                        new IncidentSuggestedActionResponse(
                            suggestedAction.Id,
                            suggestedAction.IncidentId,
                            suggestedAction.Action,
                            suggestedAction.Status,
                            suggestedAction.CreatedAtUtc,
                            suggestedAction.DecidedAtUtc,
                            suggestedAction.DecidedByUserId))
                .ToListAsync(cancellationToken);

        return Ok(suggestedActions);
    }
    [Authorize(Policy = "ResponderOrAdministrator")]
    [HttpPost("{id:guid}/suggested-actions")]
    [ProducesResponseType<IncidentSuggestedActionResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidentSuggestedActionResponse>>
        GenerateSuggestedAction(
            Guid id,
            CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                current => current.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return NotFound();
        }

        var redactedTitle =
            sensitiveDataRedactor.Redact(
                incident.Title);

        var redactedDescription =
            sensitiveDataRedactor.Redact(
                incident.Description);

        var action =
            await incidentSuggestedActionGateway
                .GenerateSuggestedActionAsync(
                    redactedTitle,
                    redactedDescription,
                    incident.Priority.ToString(),
                    incident.Status.ToString(),
                    cancellationToken);

        var suggestedAction =
            new IncidentSuggestedAction(
                incident.Id,
                action,
                DateTimeOffset.UtcNow);

        dbContext.IncidentSuggestedActions.Add(
            suggestedAction);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        var response =
            ToSuggestedActionResponse(
                suggestedAction);

        return Created(
            $"/api/incidents/{incident.Id}/suggested-actions/{suggestedAction.Id}",
            response);
    }
    [Authorize(Policy = "ResponderOrAdministrator")]
    [HttpPost("{id:guid}/suggested-actions/{actionId:guid}/approve")]
    [ProducesResponseType<IncidentSuggestedActionResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IncidentSuggestedActionResponse>>
        ApproveSuggestedAction(
            Guid id,
            Guid actionId,
            CancellationToken cancellationToken)
    {
        var suggestedAction =
            await dbContext.IncidentSuggestedActions
                .SingleOrDefaultAsync(
                    action =>
                        action.Id == actionId &&
                        action.IncidentId == id,
                    cancellationToken);

        if (suggestedAction is null)
        {
            return NotFound();
        }

        var decidedByUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(decidedByUserId))
        {
            return Unauthorized();
        }

        try
        {
            suggestedAction.Approve(
                decidedByUserId,
                DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Suggested action already decided",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ToSuggestedActionResponse(
                suggestedAction));
    }
    [Authorize(Policy = "ResponderOrAdministrator")]
    [HttpPost("{id:guid}/suggested-actions/{actionId:guid}/reject")]
    [ProducesResponseType<IncidentSuggestedActionResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IncidentSuggestedActionResponse>>
        RejectSuggestedAction(
            Guid id,
            Guid actionId,
            CancellationToken cancellationToken)
    {
        var suggestedAction =
            await dbContext.IncidentSuggestedActions
                .SingleOrDefaultAsync(
                    action =>
                        action.Id == actionId &&
                        action.IncidentId == id,
                    cancellationToken);

        if (suggestedAction is null)
        {
            return NotFound();
        }

        var decidedByUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(decidedByUserId))
        {
            return Unauthorized();
        }

        try
        {
            suggestedAction.Reject(
                decidedByUserId,
                DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Suggested action already decided",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return Ok(
            ToSuggestedActionResponse(
                suggestedAction));
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

    [HttpGet("{id:guid}/activity")]
    [ProducesResponseType<IReadOnlyList<IncidentActivityResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<IncidentActivityResponse>>> GetActivity(
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

        var incident = await incidentQuery
            .SingleOrDefaultAsync(
                current => current.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return NotFound();
        }

        var statusChanges = await dbContext.IncidentStatusChanges
            .AsNoTracking()
            .Where(change => change.IncidentId == id)
            .Select(change => new IncidentActivityResponse(
                "StatusChanged",
                change.ChangedAtUtc,
                change.ChangedByUserId,
                change.PreviousStatus,
                change.NewStatus,
                null))
            .ToListAsync(cancellationToken);

        var teamAssignments = await dbContext.IncidentTeamAssignments
            .AsNoTracking()
            .Where(assignment => assignment.IncidentId == id)
            .Select(assignment => new IncidentActivityResponse(
                "TeamAssigned",
                assignment.AssignedAtUtc,
                assignment.AssignedByUserId,
                null,
                null,
                assignment.TeamId))
            .ToListAsync(cancellationToken);

        var activity = new List<IncidentActivityResponse>
        {
            new(
                "IncidentCreated",
                incident.CreatedAtUtc,
                incident.ReporterUserId,
                null,
                null,
                null)
        };

        activity.AddRange(statusChanges);
        activity.AddRange(teamAssignments);

        var chronologicalActivity = activity
            .OrderBy(item => item.OccurredAtUtc)
            .ToList();

        return Ok(chronologicalActivity);
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

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "IncidentCreated",
            Payload = JsonSerializer.Serialize(new
            {
                incident.Id,
                incident.Title,
                incident.Priority,
                incident.ReporterUserId
            }),
            OccurredAtUtc = incident.CreatedAtUtc.UtcDateTime
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(
            "incidents:all:elevated",
            cancellationToken);

        await cache.RemoveAsync(
            $"incidents:reporter:{reporterUserId}",
            cancellationToken);

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

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "IncidentStatusChanged",
            Payload = JsonSerializer.Serialize(new
            {
                incident.Id,
                statusChange.PreviousStatus,
                statusChange.NewStatus,
                statusChange.ChangedByUserId,
                statusChange.ChangedAtUtc
            }),
            OccurredAtUtc = statusChange.ChangedAtUtc.UtcDateTime
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(
            "incidents:all:elevated",
            cancellationToken);

        await cache.RemoveAsync(
            $"incidents:reporter:{incident.ReporterUserId}",
            cancellationToken);
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

        var assignedByUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(assignedByUserId))
        {
            return Unauthorized();
        }

        var assignment = incident.AssignTeam(
            request.TeamId,
            assignedByUserId);

        dbContext.IncidentTeamAssignments.Add(assignment);

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "IncidentTeamAssigned",
            Payload = JsonSerializer.Serialize(new
            {
                incident.Id,
                assignment.TeamId,
                assignment.AssignedByUserId,
                assignment.AssignedAtUtc
            }),
            OccurredAtUtc = assignment.AssignedAtUtc.UtcDateTime
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(
            "incidents:all:elevated",
            cancellationToken);

        await cache.RemoveAsync(
            $"incidents:reporter:{incident.ReporterUserId}",
            cancellationToken);
        return Ok(ToResponse(incident));
    }

    private static IncidentSuggestedActionResponse ToSuggestedActionResponse(
        IncidentSuggestedAction suggestedAction)
    {
        return new IncidentSuggestedActionResponse(
            suggestedAction.Id,
            suggestedAction.IncidentId,
            suggestedAction.Action,
            suggestedAction.Status,
            suggestedAction.CreatedAtUtc,
            suggestedAction.DecidedAtUtc,
            suggestedAction.DecidedByUserId);
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
