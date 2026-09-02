using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace OpsPilot.Api.Tests;

public sealed class ApiProblemDetailsTests
    : IClassFixture<OpsPilotApiFactory>
{
    private readonly HttpClient _client;

    public ApiProblemDetailsTests(
        OpsPilotApiFactory factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(
                        typeof(ThrowingTestController).Assembly);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetUnknownIncident_ReturnsProblemDetails()
    {
        var token = await GetReporterTokenAsync();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token);

        var response = await _client.GetAsync(
            $"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(content);
        var problem = document.RootElement;

        Assert.Equal(
            404,
            problem.GetProperty("status").GetInt32());

        Assert.True(
            problem.TryGetProperty(
                "traceId",
                out var traceId));

        Assert.False(
            string.IsNullOrWhiteSpace(
                traceId.GetString()));
    }
    [Fact]
    public async Task UnhandledException_ReturnsSafeProblemDetails()
    {
        var response = await _client.GetAsync(
            "/__test/errors/unhandled");

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(content);
        var problem = document.RootElement;

        Assert.Equal(
            500,
            problem.GetProperty("status").GetInt32());

        Assert.Equal(
            "An unexpected error occurred.",
            problem.GetProperty("title").GetString());

        Assert.True(
            problem.TryGetProperty(
                "traceId",
                out var traceId));

        Assert.False(
            string.IsNullOrWhiteSpace(
                traceId.GetString()));

        Assert.DoesNotContain(
            "Sensitive test exception details.",
            content,
            StringComparison.OrdinalIgnoreCase);
    }
    private async Task<string> GetReporterTokenAsync()
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
}

[ApiController]
[Route("__test/errors")]
public sealed class ThrowingTestController : ControllerBase
{
    [HttpGet("unhandled")]
    public IActionResult GetUnhandled()
    {
        throw new InvalidOperationException(
            "Sensitive test exception details.");
    }
}