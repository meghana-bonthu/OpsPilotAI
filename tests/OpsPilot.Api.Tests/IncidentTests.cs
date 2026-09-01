using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Tests;

public sealed class IncidentTests
{
    [Fact]
    public void Constructor_CreatesNewIncidentWithTrimmedValues()
    {
        var incident = new Incident(
            "  Payment processing unavailable  ",
            "  Users cannot complete payment processing.  ",
            IncidentPriority.High,
            "reporter-user-1");

        Assert.NotEqual(Guid.Empty, incident.Id);
        Assert.Equal(
            "Payment processing unavailable",
            incident.Title);
        Assert.Equal(
            "Users cannot complete payment processing.",
            incident.Description);
        Assert.Equal(IncidentPriority.High, incident.Priority);
        Assert.Equal(
            "reporter-user-1",
            incident.ReporterUserId);
        Assert.Equal(IncidentStatus.New, incident.Status);
        Assert.True(incident.CreatedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(IncidentStatus.Triaged)]
    [InlineData(IncidentStatus.Cancelled)]
    public void NewIncident_AllowsExpectedTransitions(
        IncidentStatus nextStatus)
    {
        var incident = CreateIncident();

        Assert.True(incident.CanTransitionTo(nextStatus));
    }

    [Theory]
    [InlineData(IncidentStatus.InProgress)]
    [InlineData(IncidentStatus.Resolved)]
    [InlineData(IncidentStatus.Closed)]
    public void NewIncident_RejectsInvalidTransitions(
        IncidentStatus nextStatus)
    {
        var incident = CreateIncident();

        Assert.False(incident.CanTransitionTo(nextStatus));
    }

    [Fact]
    public void ChangeStatus_WhenTransitionIsValid_UpdatesStatus()
    {
        var incident = CreateIncident();

        incident.ChangeStatus(
            IncidentStatus.Triaged,
            "test-responder-user");

        Assert.Equal(IncidentStatus.Triaged, incident.Status);

        var historyEntry = Assert.Single(incident.StatusHistory);

        Assert.Equal(incident.Id, historyEntry.IncidentId);
        Assert.Equal(
            IncidentStatus.New,
            historyEntry.PreviousStatus);
        Assert.Equal(
            IncidentStatus.Triaged,
            historyEntry.NewStatus);
        Assert.Equal(
            "test-responder-user",
            historyEntry.ChangedByUserId);
        Assert.True(
            historyEntry.ChangedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ChangeStatus_WhenTransitionIsInvalid_ThrowsAndPreservesStatus()
    {
        var incident = CreateIncident();

        var exception = Assert.Throws<InvalidOperationException>(
            () => incident.ChangeStatus(
                IncidentStatus.Closed,
                "test-responder-user"));

        Assert.Equal(
            "An incident cannot transition from New to Closed.",
            exception.Message);
        Assert.Equal(IncidentStatus.New, incident.Status);
        Assert.Empty(incident.StatusHistory);
    }

    [Fact]
    public void Incident_CanCompleteFullLifecycle()
    {
        var incident = CreateIncident();

        incident.ChangeStatus(
            IncidentStatus.Triaged,
            "test-responder-user");

        incident.ChangeStatus(
            IncidentStatus.InProgress,
            "test-responder-user");

        incident.ChangeStatus(
            IncidentStatus.Resolved,
            "test-responder-user");

        incident.ChangeStatus(
            IncidentStatus.Closed,
            "test-responder-user");

        Assert.Equal(IncidentStatus.Closed, incident.Status);
        Assert.False(incident.CanTransitionTo(IncidentStatus.InProgress));
        Assert.Equal(4, incident.StatusHistory.Count);
    }
    [Fact]
    public void AssignTeam_WithValidTeamId_AssignsTeam()
    {
        var incident = CreateIncident();
        var teamId = Guid.NewGuid();
        const string assignedByUserId = "test-user";

        var assignment = incident.AssignTeam(
            teamId,
            assignedByUserId);

        Assert.Equal(teamId, incident.TeamId);

        Assert.Equal(incident.Id, assignment.IncidentId);
        Assert.Equal(teamId, assignment.TeamId);
        Assert.Equal(
            assignedByUserId,
            assignment.AssignedByUserId);

        Assert.Single(incident.TeamAssignmentHistory);
        Assert.Same(
            assignment,
            incident.TeamAssignmentHistory.Single());
    }
    [Fact]
    public void AssignTeam_WithMissingActor_Throws()
    {
        var incident = CreateIncident();
        var teamId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(
            () => incident.AssignTeam(
                teamId,
                " "));

        Assert.Equal(
            "Assigned-by user ID is required. (Parameter 'assignedByUserId')",
            exception.Message);

        Assert.Null(incident.TeamId);
        Assert.Empty(incident.TeamAssignmentHistory);
    }
    [Fact]
    public void AssignTeam_WithEmptyTeamId_Throws()
    {
        var incident = CreateIncident();

        var exception = Assert.Throws<ArgumentException>(
            () => incident.AssignTeam(
    Guid.Empty,
    "test-user"));

        Assert.Equal(
            "Team ID cannot be empty. (Parameter 'teamId')",
            exception.Message);

        Assert.Null(incident.TeamId);
    }
    [Fact]
    public void ResolvedIncident_CanBeReopened()
    {
        var incident = CreateIncident();

        incident.ChangeStatus(
            IncidentStatus.Triaged,
            "test-responder-user");

        incident.ChangeStatus(
            IncidentStatus.InProgress,
            "test-responder-user");

        incident.ChangeStatus(
            IncidentStatus.Resolved,
            "test-responder-user");

        incident.ChangeStatus(
            IncidentStatus.InProgress,
            "test-responder-user");

        Assert.Equal(IncidentStatus.InProgress, incident.Status);
    }

    private static Incident CreateIncident()
    {
        return new Incident(
            "Shipment tracking delayed",
            "Tracking events have not arrived for thirty minutes.",
            IncidentPriority.Critical,
            "test-reporter-user");
    }
}