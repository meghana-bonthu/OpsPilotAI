using System.Net;
using Xunit;

namespace OpsPilot.Api.Tests;

public sealed class HealthCheckTests
    : IClassFixture<OpsPilotApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(
        OpsPilotApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response =
            await _client.GetAsync("/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }
}