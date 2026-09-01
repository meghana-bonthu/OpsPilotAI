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
            IncidentPriority.High);

        Assert.NotEqual(Guid.Empty, incident.Id);
        Assert.Equal(
            "Payment processing unavailable",
            incident.Title);
        Assert.Equal(
            "Users cannot complete payment processing.",
            incident.Description);
        Assert.Equal(IncidentPriority.High, incident.Priority);
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

        incident.ChangeStatus(IncidentStatus.Triaged);

        Assert.Equal(IncidentStatus.Triaged, incident.Status);
        var historyEntry = Assert.Single(incident.StatusHistory);

        Assert.Equal(incident.Id, historyEntry.IncidentId);
        Assert.Equal(
            IncidentStatus.New,
            historyEntry.PreviousStatus);
        Assert.Equal(
            IncidentStatus.Triaged,
            historyEntry.NewStatus);
        Assert.True(
            historyEntry.ChangedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ChangeStatus_WhenTransitionIsInvalid_ThrowsAndPreservesStatus()
    {
        var incident = CreateIncident();

        var exception = Assert.Throws<InvalidOperationException>(
            () => incident.ChangeStatus(IncidentStatus.Closed));

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

        incident.ChangeStatus(IncidentStatus.Triaged);
        incident.ChangeStatus(IncidentStatus.InProgress);
        incident.ChangeStatus(IncidentStatus.Resolved);
        incident.ChangeStatus(IncidentStatus.Closed);

        Assert.Equal(IncidentStatus.Closed, incident.Status);
        Assert.False(incident.CanTransitionTo(IncidentStatus.InProgress));
        Assert.Equal(4, incident.StatusHistory.Count);
    }

    [Fact]
    public void ResolvedIncident_CanBeReopened()
    {
        var incident = CreateIncident();

        incident.ChangeStatus(IncidentStatus.Triaged);
        incident.ChangeStatus(IncidentStatus.InProgress);
        incident.ChangeStatus(IncidentStatus.Resolved);
        incident.ChangeStatus(IncidentStatus.InProgress);

        Assert.Equal(IncidentStatus.InProgress, incident.Status);
    }

    private static Incident CreateIncident()
    {
        return new Incident(
            "Shipment tracking delayed",
            "Tracking events have not arrived for thirty minutes.",
            IncidentPriority.Critical);
    }
}