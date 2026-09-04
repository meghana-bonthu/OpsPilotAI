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
    public DbSet<IncidentSuggestedAction> IncidentSuggestedActions =>
        Set<IncidentSuggestedAction>();
    public DbSet<ProcessedMessage> ProcessedMessages =>
        Set<ProcessedMessage>();

    public DbSet<IncidentTeamAssignment> IncidentTeamAssignments =>
        Set<IncidentTeamAssignment>();
    public DbSet<OutboxMessage> OutboxMessages =>
        Set<OutboxMessage>();
    public DbSet<Team> Teams => Set<Team>();

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

        incident.HasOne<Team>()
            .WithMany()
            .HasForeignKey(current => current.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

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

        incident.HasMany(current => current.TeamAssignmentHistory)
            .WithOne()
            .HasForeignKey(assignment => assignment.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        incident.Navigation(current => current.TeamAssignmentHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        var processedMessage =
            modelBuilder.Entity<ProcessedMessage>();

        processedMessage.HasKey(message => message.MessageId);

        var statusChange =
            modelBuilder.Entity<IncidentStatusChange>();

        statusChange.HasKey(change => change.Id);

        statusChange.Property(change => change.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        statusChange.Property(change => change.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        statusChange.Property(change => change.ChangedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        statusChange.HasIndex(change => new
        {
            change.IncidentId,
            change.ChangedAtUtc
        });

        var teamAssignment =
            modelBuilder.Entity<IncidentTeamAssignment>();

        teamAssignment.HasKey(assignment => assignment.Id);

        teamAssignment.Property(
                assignment => assignment.AssignedByUserId)
            .HasMaxLength(450)
            .IsRequired();

        teamAssignment.HasIndex(assignment => new
        {
            assignment.IncidentId,
            assignment.AssignedAtUtc
        });

        teamAssignment.HasOne<Team>()
            .WithMany()
            .HasForeignKey(assignment => assignment.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        var team = modelBuilder.Entity<Team>();

        team.HasKey(current => current.Id);

        team.Property(current => current.Name)
            .HasMaxLength(120)
            .IsRequired();

        team.HasIndex(current => current.Name)
            .IsUnique();
        
        var suggestedAction =
            modelBuilder.Entity<IncidentSuggestedAction>();

        suggestedAction.HasKey(action => action.Id);

        suggestedAction.Property(action => action.Action)
            .HasMaxLength(2000)
            .IsRequired();

        suggestedAction.Property(action => action.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        suggestedAction.Property(action => action.DecidedByUserId)
            .HasMaxLength(450);

        suggestedAction.HasIndex(action => new
        {
            action.IncidentId,
            action.CreatedAtUtc
        });

        suggestedAction.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(action => action.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        var outboxMessage =
            modelBuilder.Entity<OutboxMessage>();

        outboxMessage.HasKey(message => message.Id);

        outboxMessage.Property(message => message.Type)
            .HasMaxLength(200)
            .IsRequired();

        outboxMessage.Property(message => message.Payload)
            .IsRequired();

        outboxMessage.Property(message => message.Error)
            .HasMaxLength(2000);

        outboxMessage.HasIndex(message => new
        {
            message.ProcessedAtUtc,
            message.OccurredAtUtc
        });
    }
}
