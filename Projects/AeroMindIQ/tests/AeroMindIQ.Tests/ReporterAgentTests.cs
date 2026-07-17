using AeroMindIQ.Agents;
using AeroMindIQ.Tests.Fakes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Tests;

public class ReporterAgentTests
{
    private static readonly AnomalyContext SampleAnomaly = new(
        LineId: 2,
        WindowStart: new DateTime(2026, 7, 17, 3, 0, 0, DateTimeKind.Utc),
        WindowEnd: new DateTime(2026, 7, 17, 4, 0, 0, DateTimeKind.Utc),
        ObservedYieldPct: 68.5m,
        BaselineMeanYieldPct: 97.4m,
        BaselineStdDevYieldPct: 2.4m,
        ZScore: 12.0m);

    [Fact]
    public async Task DraftReportAsync_ReturnsReporterTextVerbatimAsync()
    {
        const string reportText = "## Summary\nYield dropped on Line 2.";
        var fakeChat = new FakeChatCompletionService(new ChatMessageContent(AuthorRole.Assistant, reportText));
        var reporter = new ReporterAgent(fakeChat, "test-model");

        var result = await reporter.DraftReportAsync(SampleAnomaly, "some query findings");

        Assert.Contains(reportText, result.ReportMarkdown);
    }

    [Fact]
    public async Task DraftReportAsync_PassesAnomalyAndFindingsIntoThePromptAsync()
    {
        var fakeChat = new FakeChatCompletionService(new ChatMessageContent(AuthorRole.Assistant, "report"));
        var reporter = new ReporterAgent(fakeChat, "test-model");

        await reporter.DraftReportAsync(SampleAnomaly, "UNIQUE_FINDINGS_MARKER_12345");

        var sentHistory = fakeChat.ReceivedHistories.Single();
        var userMessage = sentHistory.Last(m => m.Role == AuthorRole.User).Content;
        Assert.Contains("UNIQUE_FINDINGS_MARKER_12345", userMessage);
        Assert.Contains("Line 2", userMessage);
    }
}
