using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Data;

public sealed class OpsPilotDbContext(
    DbContextOptions<OpsPilotDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<IncidentStatusChange> IncidentStatusChanges =>
        Set<IncidentStatusChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var incident = modelBuilder.Entity<Incident>();

        incident.HasKey(current => current.Id);

        incident.Property(current => current.Title)
            .HasMaxLength(160)
            .IsRequired();

        incident.Property(current => current.Description)
            .HasMaxLength(4000)
            .IsRequired();

        incident.Property(current => current.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        incident.Property(current => current.ReporterUserId)
            .HasMaxLength(450)
            .IsRequired();

        incident.Property(current => current.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        incident.HasIndex(current => new
        {
            current.Status,
            current.Priority
        });

        incident.HasMany(current => current.StatusHistory)
            .WithOne()
            .HasForeignKey(change => change.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        incident.Navigation(current => current.StatusHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var statusChange = modelBuilder.Entity<IncidentStatusChange>();

        statusChange.HasKey(change => change.Id);

        statusChange.Property(change => change.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        statusChange.Property(change => change.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        statusChange.HasIndex(change => new
        {
            change.IncidentId,
            change.ChangedAtUtc
        });
    }
}