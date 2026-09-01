using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
        var token = await CreateResponderTokenAsync();

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

        Assert.NotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        Assert.NotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
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
    private async Task<string> CreateResponderTokenAsync()
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

        return token.Token;
    }
}