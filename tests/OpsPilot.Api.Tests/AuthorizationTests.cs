using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.AI;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;
using OpsPilot.Api.Security;

namespace OpsPilot.Api.Tests;

public sealed class AuthorizationTests
    : IClassFixture<OpsPilotApiFactory>
{
    private readonly OpsPilotApiFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(
        OpsPilotApiFactory factory)
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
    public async Task GetSummary_AsOwner_ReturnsSummary()
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
                title = "Database connectivity issue",
                description = "Users are unable to connect to the database.",
                priority = "High"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var response = await _client.GetAsync(
            $"/api/incidents/{incidentId}/summary");

        response.EnsureSuccessStatusCode();

        using var summaryDocument = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            incidentId,
            summaryDocument.RootElement
                .GetProperty("incidentId")
                .GetGuid());

        Assert.Contains(
            "Database connectivity issue",
            summaryDocument.RootElement
                .GetProperty("summary")
                .GetString());

        Assert.Equal(
            AiPromptVersions.IncidentSummary,
            summaryDocument.RootElement
                .GetProperty("promptVersion")
                .GetString());
    }
    [Fact]
    public async Task GetSummary_AsDifferentReporter_ReturnsNotFound()
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
                title = "AI summary ownership test",
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
            $"/api/incidents/{incidentId}/summary");

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
    [Fact]
    public async Task GenerateSuggestedAction_AsReporter_ReturnsForbidden()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var response = await _client.PostAsync(
            $"/api/incidents/{Guid.NewGuid()}/suggested-actions",
            null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }
    [Fact]
    public async Task GenerateSuggestedAction_AsResponder_CreatesPendingSuggestion()
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
                title = "Payment gateway timeout",
                description = "Transactions are timing out.",
                priority = "High"
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

        var response = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            incidentId,
            document.RootElement
                .GetProperty("incidentId")
                .GetGuid());

        Assert.Equal(
            "Pending",
            document.RootElement
                .GetProperty("status")
                .GetString());

        Assert.Equal(
            AiPromptVersions.IncidentSuggestedAction,
            document.RootElement
                .GetProperty("promptVersion")
                .GetString());

        Assert.False(
            string.IsNullOrWhiteSpace(
                document.RootElement
                    .GetProperty("action")
                    .GetString()));
    }
    [Fact]
    public async Task ApproveSuggestedAction_AsResponder_RecordsDecision()
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
                title = "Approval workflow integration test",
                description = "Verify a responder can approve an AI suggestion.",
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

        var generateResponse = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions",
            null);

        generateResponse.EnsureSuccessStatusCode();

        using var generateDocument = JsonDocument.Parse(
            await generateResponse.Content.ReadAsStringAsync());

        var actionId =
            generateDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var approveResponse = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions/{actionId}/approve",
            null);

        approveResponse.EnsureSuccessStatusCode();

        using var approveDocument = JsonDocument.Parse(
            await approveResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Approved",
            approveDocument.RootElement
                .GetProperty("status")
                .GetString());

        Assert.Equal(
            responder.UserId,
            approveDocument.RootElement
                .GetProperty("decidedByUserId")
                .GetString());

        Assert.NotEqual(
            JsonValueKind.Null,
            approveDocument.RootElement
                .GetProperty("decidedAtUtc")
                .ValueKind);
    }
    [Fact]
    public async Task RejectSuggestedAction_AsResponder_RecordsDecision()
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
                title = "Rejection workflow integration test",
                description = "Verify a responder can reject an AI suggestion.",
                priority = "Low"
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

        var generateResponse = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions",
            null);

        generateResponse.EnsureSuccessStatusCode();

        using var generateDocument = JsonDocument.Parse(
            await generateResponse.Content.ReadAsStringAsync());

        var actionId =
            generateDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var rejectResponse = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions/{actionId}/reject",
            null);

        rejectResponse.EnsureSuccessStatusCode();

        using var rejectDocument = JsonDocument.Parse(
            await rejectResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Rejected",
            rejectDocument.RootElement
                .GetProperty("status")
                .GetString());

        Assert.Equal(
            responder.UserId,
            rejectDocument.RootElement
                .GetProperty("decidedByUserId")
                .GetString());

        Assert.NotEqual(
            JsonValueKind.Null,
            rejectDocument.RootElement
                .GetProperty("decidedAtUtc")
                .ValueKind);
    }
    [Fact]
    public async Task ApproveSuggestedAction_WhenAlreadyDecided_ReturnsConflict()
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
                title = "Double decision integration test",
                description = "Verify a decided suggestion cannot be changed again.",
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

        var generateResponse = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions",
            null);

        generateResponse.EnsureSuccessStatusCode();

        using var generateDocument = JsonDocument.Parse(
            await generateResponse.Content.ReadAsStringAsync());

        var actionId =
            generateDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var firstDecisionResponse = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions/{actionId}/reject",
            null);

        firstDecisionResponse.EnsureSuccessStatusCode();

        var secondDecisionResponse = await _client.PostAsync(
            $"/api/incidents/{incidentId}/suggested-actions/{actionId}/approve",
            null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondDecisionResponse.StatusCode);
    }
    [Fact]
    public async Task Search_WhenSemanticSearchUnavailable_UsesTextFallback()
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
                title = "Database connection timeout",
                description = "Primary database cannot be reached.",
                priority = "High"
            });

        createResponse.EnsureSuccessStatusCode();

        using var createDocument = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var incidentId =
            createDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var searchResponse = await _client.GetAsync(
            "/api/incidents/search?query=database");

        searchResponse.EnsureSuccessStatusCode();

        Assert.True(
            searchResponse.Headers.TryGetValues(
                "X-OpsPilot-Search-Mode",
                out var searchModes));

        Assert.Equal(
            "fallback",
            Assert.Single(searchModes));

        Assert.False(
            searchResponse.Headers.Contains(
                "X-OpsPilot-AI-Version"));

        using var searchDocument = JsonDocument.Parse(
            await searchResponse.Content.ReadAsStringAsync());

        var results =
            searchDocument.RootElement;

        Assert.Contains(
            results.EnumerateArray(),
            incident =>
                incident.GetProperty("id").GetGuid() ==
                incidentId);
    }
    [Fact]
    public async Task Search_ReporterOnlySeesOwnIncidents()
    {
        var firstReporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                firstReporterToken);

        var firstCreateResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Shared search keyword",
                description = "First reporter incident.",
                priority = "Medium"
            });

        firstCreateResponse.EnsureSuccessStatusCode();

        using var firstCreateDocument = JsonDocument.Parse(
            await firstCreateResponse.Content.ReadAsStringAsync());

        var firstIncidentId =
            firstCreateDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var secondReporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                secondReporterToken);

        var secondCreateResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Shared search keyword",
                description = "Second reporter incident.",
                priority = "Medium"
            });

        secondCreateResponse.EnsureSuccessStatusCode();

        using var secondCreateDocument = JsonDocument.Parse(
            await secondCreateResponse.Content.ReadAsStringAsync());

        var secondIncidentId =
            secondCreateDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                firstReporterToken);

        var searchResponse = await _client.GetAsync(
            "/api/incidents/search?query=Shared");

        searchResponse.EnsureSuccessStatusCode();

        using var searchDocument = JsonDocument.Parse(
            await searchResponse.Content.ReadAsStringAsync());

        var results =
            searchDocument.RootElement
                .EnumerateArray()
                .Select(
                    incident =>
                        incident.GetProperty("id").GetGuid())
                .ToList();

        Assert.Contains(firstIncidentId, results);
        Assert.DoesNotContain(secondIncidentId, results);
    }
    [Fact]
    public async Task Search_WithBlankQuery_ReturnsBadRequest()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var response = await _client.GetAsync(
            "/api/incidents/search?query=%20%20%20");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Search query is required",
            document.RootElement
                .GetProperty("title")
                .GetString());

        Assert.Equal(
            400,
            document.RootElement
                .GetProperty("status")
                .GetInt32());
    }
    [Fact]
    public async Task Search_WhenSemanticGatewayReturnsIds_PreservesSemanticRanking()
    {
        var reporterToken =
            await RegisterReporterAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var firstCreateResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "First semantic incident",
                description = "Semantic ranking candidate one.",
                priority = "Medium"
            });

        firstCreateResponse.EnsureSuccessStatusCode();

        using var firstCreateDocument = JsonDocument.Parse(
            await firstCreateResponse.Content.ReadAsStringAsync());

        var firstIncidentId =
            firstCreateDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var secondCreateResponse = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Second semantic incident",
                description = "Semantic ranking candidate two.",
                priority = "Medium"
            });

        secondCreateResponse.EnsureSuccessStatusCode();

        using var secondCreateDocument = JsonDocument.Parse(
            await secondCreateResponse.Content.ReadAsStringAsync());

        var secondIncidentId =
            secondCreateDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        using var semanticFactory =
            _factory.WithWebHostBuilder(
                builder =>
                {
                    builder.ConfigureServices(
                        services =>
                        {
                            services.RemoveAll<
                                IIncidentSemanticSearchGateway>();

                            services.AddSingleton<
                                IIncidentSemanticSearchGateway>(
                                new TestIncidentSemanticSearchGateway(
                                    new[]
                                    {
                                        firstIncidentId,
                                        secondIncidentId
                                    }));
                        });
                });

        using var semanticClient =
            semanticFactory.CreateClient();

        semanticClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                reporterToken);

        var searchResponse = await semanticClient.GetAsync(
            "/api/incidents/search?query=semantic");

        searchResponse.EnsureSuccessStatusCode();

        Assert.True(
            searchResponse.Headers.TryGetValues(
                "X-OpsPilot-Search-Mode",
                out var searchModes));

        Assert.Equal(
            "semantic",
            Assert.Single(searchModes));

        Assert.True(
            searchResponse.Headers.TryGetValues(
                "X-OpsPilot-AI-Version",
                out var aiVersions));

        Assert.Equal(
            AiPromptVersions.IncidentSemanticSearch,
            Assert.Single(aiVersions));

        using var searchDocument = JsonDocument.Parse(
            await searchResponse.Content.ReadAsStringAsync());

        var resultIds =
            searchDocument.RootElement
                .EnumerateArray()
                .Select(
                    incident =>
                        incident.GetProperty("id").GetGuid())
                .ToList();

        Assert.Equal(
            new[]
            {
                firstIncidentId,
                secondIncidentId
            },
            resultIds);
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

    private sealed class TestIncidentSemanticSearchGateway(
        IReadOnlyList<Guid> resultIds)
        : IIncidentSemanticSearchGateway
    {
        public Task<IReadOnlyList<Guid>?> SearchAsync(
            string query,
            IReadOnlyList<IncidentSearchDocument> incidents,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<
                IReadOnlyList<Guid>?>(resultIds);
        }
    }
}
