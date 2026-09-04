using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpsPilot.Api.Data;

namespace OpsPilot.Api.Health;

public sealed class DatabaseHealthCheck(
    IServiceScopeFactory scopeFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope =
                scopeFactory.CreateAsyncScope();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<OpsPilotDbContext>();

            var canConnect =
                await dbContext.Database.CanConnectAsync(
                    cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(
                    "Database connection failed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Database readiness check failed.",
                exception);
        }
    }
}