using OpsPilot.Api.AI;

using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Tests;

public sealed class IncidentSuggestedActionTests
{
    [Fact]
    public void Constructor_CreatesPendingSuggestedAction()
    {
        var incidentId = Guid.NewGuid();
        var createdAtUtc = DateTimeOffset.UtcNow;

        var suggestedAction = new IncidentSuggestedAction(
            incidentId,
            "  Restart the application service.  ",
            AiPromptVersions.IncidentSuggestedAction,
            createdAtUtc);

        Assert.NotEqual(
            Guid.Empty,
            suggestedAction.Id);

        Assert.Equal(
            incidentId,
            suggestedAction.IncidentId);

        Assert.Equal(
            "Restart the application service.",
            suggestedAction.Action);

        Assert.Equal(
            AiPromptVersions.IncidentSuggestedAction,
            suggestedAction.PromptVersion);

        Assert.Equal(
            SuggestedActionStatus.Pending,
            suggestedAction.Status);

        Assert.Equal(
            createdAtUtc,
            suggestedAction.CreatedAtUtc);

        Assert.Null(
            suggestedAction.DecidedAtUtc);

        Assert.Null(
            suggestedAction.DecidedByUserId);
    }

    [Fact]
    public void Constructor_WithMissingPromptVersion_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new IncidentSuggestedAction(
                Guid.NewGuid(),
                "Restart the application service.",
                " ",
                DateTimeOffset.UtcNow));

        Assert.Equal(
            "Prompt version is required. (Parameter 'promptVersion')",
            exception.Message);
    }

    [Fact]
    public void Approve_WhenPending_RecordsDecision()
    {
        var suggestedAction = CreateSuggestedAction();
        var decidedAtUtc = DateTimeOffset.UtcNow;

        suggestedAction.Approve(
            "responder-user-1",
            decidedAtUtc);

        Assert.Equal(
            SuggestedActionStatus.Approved,
            suggestedAction.Status);

        Assert.Equal(
            "responder-user-1",
            suggestedAction.DecidedByUserId);

        Assert.Equal(
            decidedAtUtc,
            suggestedAction.DecidedAtUtc);
    }

    [Fact]
    public void Reject_WhenPending_RecordsDecision()
    {
        var suggestedAction = CreateSuggestedAction();
        var decidedAtUtc = DateTimeOffset.UtcNow;

        suggestedAction.Reject(
            "responder-user-1",
            decidedAtUtc);

        Assert.Equal(
            SuggestedActionStatus.Rejected,
            suggestedAction.Status);

        Assert.Equal(
            "responder-user-1",
            suggestedAction.DecidedByUserId);

        Assert.Equal(
            decidedAtUtc,
            suggestedAction.DecidedAtUtc);
    }

    [Fact]
    public void Approve_WhenAlreadyRejected_Throws()
    {
        var suggestedAction = CreateSuggestedAction();

        suggestedAction.Reject(
            "responder-user-1",
            DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(
            () => suggestedAction.Approve(
                "responder-user-2",
                DateTimeOffset.UtcNow));

        Assert.Equal(
            "Only pending suggested actions can be decided.",
            exception.Message);

        Assert.Equal(
            SuggestedActionStatus.Rejected,
            suggestedAction.Status);
    }

    [Fact]
    public void Decide_WithMissingActor_Throws()
    {
        var suggestedAction = CreateSuggestedAction();

        var exception = Assert.Throws<ArgumentException>(
            () => suggestedAction.Approve(
                " ",
                DateTimeOffset.UtcNow));

        Assert.Equal(
            "Deciding user ID is required. (Parameter 'decidedByUserId')",
            exception.Message);

        Assert.Equal(
            SuggestedActionStatus.Pending,
            suggestedAction.Status);

        Assert.Null(
            suggestedAction.DecidedAtUtc);

        Assert.Null(
            suggestedAction.DecidedByUserId);
    }

    private static IncidentSuggestedAction CreateSuggestedAction()
    {
        return new IncidentSuggestedAction(
            Guid.NewGuid(),
            "Restart the application service.",
            AiPromptVersions.IncidentSuggestedAction,
            DateTimeOffset.UtcNow);
    }
}
