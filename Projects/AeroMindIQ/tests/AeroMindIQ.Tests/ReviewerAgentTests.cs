using AeroMindIQ.Agents;
using AeroMindIQ.Tests.Fakes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Tests;

public class ReviewerAgentTests
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
    public async Task ApprovedResponse_ReturnsApprovedTrueAsync()
    {
        var fakeChat = new FakeChatCompletionService(
            new ChatMessageContent(AuthorRole.Assistant, "APPROVED\nQuery looks correct."));
        var reviewer = new ReviewerAgent(fakeChat, "test-model");

        var verdict = await reviewer.ReviewAsync("SELECT * FROM production_runs", "schema", SampleAnomaly);

        Assert.True(verdict.Approved);
        Assert.Contains("Query looks correct.", verdict.Feedback);
    }

    [Fact]
    public async Task RejectedResponse_ReturnsApprovedFalseWithFeedbackAsync()
    {
        var fakeChat = new FakeChatCompletionService(
            new ChatMessageContent(AuthorRole.Assistant, "REJECTED\nWrong line_id used in WHERE clause."));
        var reviewer = new ReviewerAgent(fakeChat, "test-model");

        var verdict = await reviewer.ReviewAsync("SELECT * FROM production_runs WHERE line_id = 1", "schema", SampleAnomaly);

        Assert.False(verdict.Approved);
        Assert.Contains("Wrong line_id", verdict.Feedback);
    }

    [Fact]
    public async Task ApprovedResponse_IsCaseInsensitiveAsync()
    {
        var fakeChat = new FakeChatCompletionService(
            new ChatMessageContent(AuthorRole.Assistant, "approved\nfine."));
        var reviewer = new ReviewerAgent(fakeChat, "test-model");

        var verdict = await reviewer.ReviewAsync("SELECT 1", "schema", SampleAnomaly);

        Assert.True(verdict.Approved);
    }
}
