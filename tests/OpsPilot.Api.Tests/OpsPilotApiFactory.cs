using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Data;
using Testcontainers.MsSql;

namespace OpsPilot.Api.Tests;

public sealed class OpsPilotApiFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<OpsPilotDbContext>()
            .UseSqlServer(_sqlContainer.GetConnectionString())
            .Options;

        await using var dbContext =
            new OpsPilotDbContext(options);

        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:OpsPilot",
            _sqlContainer.GetConnectionString());

        builder.UseSetting(
            "Jwt:Key",
            Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    "OpsPilotAI-Test-Only-JWT-Signing-Key-2026")));

        builder.UseSetting(
            "Jwt:Issuer",
            "OpsPilot.Api");

        builder.UseSetting(
            "Jwt:Audience",
            "OpsPilot.Client");
    }
}
