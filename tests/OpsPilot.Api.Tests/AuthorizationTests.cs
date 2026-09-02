using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;
using OpsPilot.Api.Security;

namespace OpsPilot.Api.Tests;

public sealed class AuthorizationTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(
        WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetIncidents_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(
            "/api/incidents");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsReporter_ReturnsForbidden()
    {
        var token = await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        var response = await _client.PatchAsJsonAsync(
            $"/api/incidents/{Guid.NewGuid()}/status",
            new
            {
                status = "InProgress"
            });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
    [Fact]
    public async Task UpdateStatus_AsResponder_IsAuthorized()
    {
        var responder = await CreateResponderTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                responder.Token);

        var response = await _client.PatchAsJsonAsync(
            $"/api/incidents/{Guid.NewGuid()}/status",
            new
            {
                status = "InProgress"
            });

        Assert.NotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        Assert.NotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
    [Fact]
    public async Task UpdateStatus_AsResponder_RecordsActorInHistory()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Audit actor integration test",
                description = "Verify responder identity is recorded.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var responder =
            await CreateResponderTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                responder.Token);

        var updateResponse = await _client.PatchAsJsonAsync(
            $"/api/incidents/{incidentId}/status",
            new
            {
                status = "Triaged"
            });

        updateResponse.EnsureSuccessStatusCode();

        var historyResponse = await _client.GetAsync(
            $"/api/incidents/{incidentId}/history");

        historyResponse.EnsureSuccessStatusCode();

        using var historyDocument = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        var historyEntry =
            historyDocument.RootElement
                .EnumerateArray()
                .Single();

        Assert.Equal(
            responder.UserId,
            historyEntry
                .GetProperty("changedByUserId")
                .GetString());
    }
    [Fact]
    public async Task GetIncident_AsDifferentReporter_ReturnsNotFound()
    {
        var reporterAToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterAToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Reporter ownership test",
                description = "Created by Reporter A.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var reporterBToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterBToken);

        var response = await _client.GetAsync(
            $"/api/incidents/{incidentId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task GetIncidents_AsReporter_ReturnsOnlyOwnedIncidents()
    {
        var reporterAToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterAToken);

        var createAResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Reporter A incident",
                description = "Owned by Reporter A.",
                priority = "Low"
            });

        createAResponse.EnsureSuccessStatusCode();

        var reporterBToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterBToken);

        var createBResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Reporter B incident",
                description = "Owned by Reporter B.",
                priority = "High"
            });

        createBResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            "/api/incidents");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var incidents = document.RootElement;

        Assert.Contains(
            incidents.EnumerateArray(),
            incident =>
                incident.GetProperty("title").GetString()
                == "Reporter B incident");

        Assert.DoesNotContain(
            incidents.EnumerateArray(),
            incident =>
                incident.GetProperty("title").GetString()
                == "Reporter A incident");
    }
    [Fact]
    public async Task GetHistory_AsDifferentReporter_ReturnsNotFound()
    {
        var reporterAToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterAToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "History ownership test",
                description = "Created by Reporter A.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var reporterBToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterBToken);

        var response = await _client.GetAsync(
            $"/api/incidents/{incidentId}/history");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task GetActivity_ReturnsChronologicalIncidentActivity()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Activity history integration test",
                description = "Verify chronological incident activity history.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        Guid teamId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<OpsPilotDbContext>();

            var team = new Team(
                $"Activity-{Guid.NewGuid():N}");

            dbContext.Teams.Add(team);

            await dbContext.SaveChangesAsync();

            teamId = team.Id;
        }

        var responder =
            await CreateResponderTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                responder.Token);

        var assignmentResponse =
            await _client.PatchAsJsonAsync(
                $"/api/incidents/{incidentId}/team",
                new
                {
                    teamId
                });

        assignmentResponse.EnsureSuccessStatusCode();

        var statusResponse =
            await _client.PatchAsJsonAsync(
                $"/api/incidents/{incidentId}/status",
                new
                {
                    status = "Triaged"
                });

        statusResponse.EnsureSuccessStatusCode();

        var activityResponse = await _client.GetAsync(
            $"/api/incidents/{incidentId}/activity");

        activityResponse.EnsureSuccessStatusCode();

        using var activityDocument = JsonDocument.Parse(
            await activityResponse.Content.ReadAsStringAsync());

        var activity =
            activityDocument.RootElement
                .EnumerateArray()
                .ToArray();

        Assert.Equal(3, activity.Length);

        Assert.Equal(
            "IncidentCreated",
            activity[0]
                .GetProperty("type")
                .GetString());

        Assert.Equal(
            "TeamAssigned",
            activity[1]
                .GetProperty("type")
                .GetString());

        Assert.Equal(
            teamId,
            activity[1]
                .GetProperty("teamId")
                .GetGuid());

        Assert.Equal(
            responder.UserId,
            activity[1]
                .GetProperty("actorUserId")
                .GetString());

        Assert.Equal(
            "StatusChanged",
            activity[2]
                .GetProperty("type")
                .GetString());

        Assert.Equal(
            "New",
            activity[2]
                .GetProperty("previousStatus")
                .GetString());

        Assert.Equal(
            "Triaged",
            activity[2]
                .GetProperty("newStatus")
                .GetString());

        Assert.Equal(
            responder.UserId,
            activity[2]
                .GetProperty("actorUserId")
                .GetString());

        var occurredAtUtc = activity
            .Select(item =>
                item.GetProperty("occurredAtUtc").GetDateTimeOffset())
            .ToArray();

        Assert.Equal(
            occurredAtUtc.OrderBy(value => value),
            occurredAtUtc);
    }
    [Fact]
    public async Task GetActivity_AsDifferentReporter_ReturnsNotFound()
    {
        var reporterAToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterAToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Activity ownership test",
                description = "Created by Reporter A.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var reporterBToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterBToken);

        var response = await _client.GetAsync(
            $"/api/incidents/{incidentId}/activity");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task AssignTeam_AsResponder_AssignsExistingTeam()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Team assignment integration test",
                description = "Verify responder can assign an existing team.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        Guid teamId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<OpsPilotDbContext>();

            var team = new Team(
                $"Operations-{Guid.NewGuid():N}");

            dbContext.Teams.Add(team);

            await dbContext.SaveChangesAsync();

            teamId = team.Id;
        }

        var responder =
            await CreateResponderTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                responder.Token);

        var assignmentResponse =
            await _client.PatchAsJsonAsync(
                $"/api/incidents/{incidentId}/team",
                new
                {
                    teamId
                });

        assignmentResponse.EnsureSuccessStatusCode();

        using var assignmentDocument = JsonDocument.Parse(
            await assignmentResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            teamId,
            assignmentDocument.RootElement
                .GetProperty("teamId")
                .GetGuid());

        var getResponse = await _client.GetAsync(
            $"/api/incidents/{incidentId}");

        getResponse.EnsureSuccessStatusCode();

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            teamId,
            getDocument.RootElement
                .GetProperty("teamId")
                .GetGuid());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<OpsPilotDbContext>();

            var assignment = await dbContext.IncidentTeamAssignments
                .AsNoTracking()
                .SingleAsync(
                    current =>
                        current.IncidentId == incidentId &&
                        current.TeamId == teamId);

            Assert.Equal(
                responder.UserId,
                assignment.AssignedByUserId);
        }
    }
    [Fact]
    public async Task AssignTeam_AsReporter_ReturnsForbidden()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var response =
            await _client.PatchAsJsonAsync(
                $"/api/incidents/{Guid.NewGuid()}/team",
                new
                {
                    teamId = Guid.NewGuid()
                });

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
    [Fact]
    public async Task AssignTeam_WithUnknownTeam_ReturnsNotFound()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Unknown team assignment test",
                description = "Verify assignment fails for a missing team.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var responder =
            await CreateResponderTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                responder.Token);

        var response =
            await _client.PatchAsJsonAsync(
                $"/api/incidents/{incidentId}/team",
                new
                {
                    teamId = Guid.NewGuid()
                });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
    [Fact]
    public async Task AssignTeam_WithEmptyTeamId_ReturnsBadRequest()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Empty team assignment test",
                description = "Verify an empty team ID is rejected.",
                priority = "Medium"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var responder =
            await CreateResponderTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                responder.Token);

        var response =
            await _client.PatchAsJsonAsync(
                $"/api/incidents/{incidentId}/team",
                new
                {
                    teamId = Guid.Empty
                });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
    [Fact]
    public async Task CreateTeam_AsAdministrator_CreatesTeam()
    {
        var administratorToken =
            await CreateAdministratorTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                administratorToken);

        var teamName =
            $"Platform-{Guid.NewGuid():N}";

        var response = await _client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                name = teamName
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            teamName,
            document.RootElement
                .GetProperty("name")
                .GetString());

        Assert.NotEqual(
            Guid.Empty,
            document.RootElement
                .GetProperty("id")
                .GetGuid());
    }
    [Fact]
    public async Task GetTeams_AsAdministrator_ReturnsTeams()
    {
        var administratorToken =
            await CreateAdministratorTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                administratorToken);

        var teamName =
            $"Support-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                name = teamName
            });

        createResponse.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            "/api/teams");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Contains(
            document.RootElement.EnumerateArray(),
            team =>
                team.GetProperty("name").GetString()
                == teamName);
    }
    [Fact]
    public async Task GetTeams_AsReporter_ReturnsForbidden()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var response = await _client.GetAsync(
            "/api/teams");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
    [Fact]
    public async Task CreateTeam_WithDuplicateName_ReturnsConflict()
    {
        var administratorToken =
            await CreateAdministratorTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                administratorToken);

        var teamName =
            $"Duplicate-{Guid.NewGuid():N}";

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                name = teamName
            });

        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/teams",
            new
            {
                name = teamName
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);
    }
    private async Task<string> RegisterReporterAsync()
    {
        var email =
            $"reporter-{Guid.NewGuid():N}@opspilot.local";

        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password = "Reporter1!"
            });

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("accessToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "Access token was not returned.");
    }
    private async Task<(string Token, string UserId)> CreateResponderTokenAsync()
    {
        using var scope =
            _factory.Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        var tokenService =
            scope.ServiceProvider
                .GetRequiredService<
                    JwtTokenService>();

        var email =
            $"responder-{Guid.NewGuid():N}@opspilot.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        var createResult =
            await userManager.CreateAsync(
                user,
                "Responder1!");

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    createResult.Errors.Select(
                        error => error.Description)));
        }

        var roleResult =
            await userManager.AddToRoleAsync(
                user,
                "Responder");

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    roleResult.Errors.Select(
                        error => error.Description)));
        }

        var token =
            await tokenService.CreateTokenAsync(user);

        return (token.Token, user.Id);
    }
    private async Task<string> CreateAdministratorTokenAsync()
    {
        using var scope =
            _factory.Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<
                    UserManager<ApplicationUser>>();

        var tokenService =
            scope.ServiceProvider
                .GetRequiredService<
                    JwtTokenService>();

        var email =
            $"admin-{Guid.NewGuid():N}@opspilot.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email
        };

        var createResult =
            await userManager.CreateAsync(
                user,
                "Administrator1!");

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    createResult.Errors.Select(
                        error => error.Description)));
        }

        var roleResult =
            await userManager.AddToRoleAsync(
                user,
                "Administrator");

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    roleResult.Errors.Select(
                        error => error.Description)));
        }

        var token =
            await tokenService.CreateTokenAsync(user);

        return token.Token;
    }
}