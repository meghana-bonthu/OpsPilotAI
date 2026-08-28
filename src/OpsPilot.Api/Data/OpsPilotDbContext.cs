using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Data;

public sealed class OpsPilotDbContext(DbContextOptions<OpsPilotDbContext> options) : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var incident = modelBuilder.Entity<Incident>();
        incident.HasKey(x => x.Id);
        incident.Property(x => x.Title).HasMaxLength(160).IsRequired();
        incident.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        incident.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        incident.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        incident.HasIndex(x => new { x.Status, x.Priority });
    }
}
