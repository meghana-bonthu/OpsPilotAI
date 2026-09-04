using System.Text.Json;
using OpsPilot.Api.AI;

namespace OpsPilot.Api.Tests;

public sealed class AiEvaluationDatasetTests
{
    [Fact]
    public async Task EvaluationDataset_MeetsExpectedResults()
    {
        var datasetPath = Path.Combine(
            AppContext.BaseDirectory,
            "Evaluation",
            "incident-ai-evaluation.json");

        Assert.True(
            File.Exists(datasetPath),
            $"Evaluation dataset was not found at {datasetPath}.");

        using var document =
            JsonDocument.Parse(
                await File.ReadAllTextAsync(datasetPath));

        var root = document.RootElement;

        Assert.Equal(
            "incident-ai-eval-v1",
            root.GetProperty("datasetVersion").GetString());

        var redactor = new SensitiveDataRedactor();
        var summaryGateway = new LocalIncidentSummaryGateway();
        var suggestedActionGateway =
            new LocalIncidentSuggestedActionGateway();
        var semanticSearchGateway =
            new LocalIncidentSemanticSearchGateway();

        foreach (var evaluationCase in root.GetProperty("cases")
                     .EnumerateArray())
        {
            var feature =
                evaluationCase.GetProperty("feature").GetString();

            var promptVersion =
                evaluationCase
                    .GetProperty("promptVersion")
                    .GetString();

            switch (feature)
            {
                case "incident-summary":
                {
                    Assert.Equal(
                        AiPromptVersions.IncidentSummary,
                        promptVersion);

                    var input =
                        evaluationCase.GetProperty("input");

                    var summary =
                        await summaryGateway.GenerateSummaryAsync(
                            redactor.Redact(
                                input.GetProperty("title").GetString()!),
                            redactor.Redact(
                                input.GetProperty("description").GetString()!),
                            input.GetProperty("priority").GetString()!,
                            CancellationToken.None);

                    AssertExpectedText(
                        summary,
                        evaluationCase.GetProperty("expected"));

                    break;
                }

                case "incident-suggested-action":
                {
                    Assert.Equal(
                        AiPromptVersions.IncidentSuggestedAction,
                        promptVersion);

                    var input =
                        evaluationCase.GetProperty("input");

                    var action =
                        await suggestedActionGateway
                            .GenerateSuggestedActionAsync(
                                redactor.Redact(
                                    input.GetProperty("title").GetString()!),
                                redactor.Redact(
                                    input.GetProperty("description").GetString()!),
                                input.GetProperty("priority").GetString()!,
                                input.GetProperty("status").GetString()!,
                                CancellationToken.None);

                    AssertExpectedText(
                        action,
                        evaluationCase.GetProperty("expected"));

                    break;
                }

                case "incident-semantic-search":
                {
                    Assert.Equal(
                        AiPromptVersions.IncidentSemanticSearch,
                        promptVersion);

                    var input =
                        evaluationCase.GetProperty("input");

                    var documents =
                        input.GetProperty("documents")
                            .EnumerateArray()
                            .Select(
                                item => new IncidentSearchDocument(
                                    Guid.Parse(
                                        item.GetProperty("id").GetString()!),
                                    redactor.Redact(
                                        item.GetProperty("title").GetString()!),
                                    redactor.Redact(
                                        item.GetProperty("description").GetString()!)))
                            .ToList();

                    var results =
                        await semanticSearchGateway.SearchAsync(
                            input.GetProperty("query").GetString()!,
                            documents,
                            CancellationToken.None);

                    Assert.NotNull(results);
                    Assert.NotEmpty(results);

                    var expectedFirstId =
                        Guid.Parse(
                            evaluationCase
                                .GetProperty("expected")
                                .GetProperty("firstResultId")
                                .GetString()!);

                    Assert.Equal(
                        expectedFirstId,
                        results[0]);

                    break;
                }

                default:
                    throw new InvalidOperationException(
                        $"Unknown evaluation feature: {feature}");
            }
        }
    }

    private static void AssertExpectedText(
        string actual,
        JsonElement expected)
    {
        if (expected.TryGetProperty(
                "mustContain",
                out var mustContain))
        {
            foreach (var item in mustContain.EnumerateArray())
            {
                Assert.Contains(
                    item.GetString()!,
                    actual,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        if (expected.TryGetProperty(
                "mustNotContain",
                out var mustNotContain))
        {
            foreach (var item in mustNotContain.EnumerateArray())
            {
                Assert.DoesNotContain(
                    item.GetString()!,
                    actual,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}