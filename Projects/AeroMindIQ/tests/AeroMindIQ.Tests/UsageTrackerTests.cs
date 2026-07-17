using AeroMindIQ.Agents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Tests;

public class UsageTrackerTests
{
    [Fact]
    public void Record_NullSample_IsNoOp()
    {
        var tracker = new UsageTracker();

        tracker.Record(null);

        Assert.Equal(0, tracker.TotalPromptTokens);
        Assert.Equal(0, tracker.TotalCompletionTokens);
        Assert.Equal(0m, tracker.EstimatedCostUsd);
    }

    [Fact]
    public void Record_KnownModel_ComputesCostFromPricingTable()
    {
        var tracker = new UsageTracker();

        // claude-sonnet-5: $3.00/1M input, $15.00/1M output
        tracker.Record(new UsageSample("Fetcher", "claude-sonnet-5", PromptTokens: 1_000_000, CompletionTokens: 1_000_000));

        Assert.Equal(18.00m, tracker.EstimatedCostUsd);
    }

    [Fact]
    public void Record_MultipleSamples_AccumulatesTotals()
    {
        var tracker = new UsageTracker();

        tracker.Record(new UsageSample("Fetcher", "claude-sonnet-5", 100, 50));
        tracker.Record(new UsageSample("Reporter", "claude-sonnet-5", 200, 75));

        Assert.Equal(300, tracker.TotalPromptTokens);
        Assert.Equal(125, tracker.TotalCompletionTokens);
    }

    [Fact]
    public void Record_UnknownModel_ExcludesCostButFlagsItInSummary()
    {
        var tracker = new UsageTracker();

        tracker.Record(new UsageSample("Fetcher", "some-future-model", 1_000_000, 1_000_000));

        Assert.Equal(0m, tracker.EstimatedCostUsd);
        Assert.Contains("some-future-model", tracker.Summarize());
        Assert.Contains("cost for those calls is excluded", tracker.Summarize());
    }

    [Fact]
    public void Summarize_GroupsTokensByAgent()
    {
        var tracker = new UsageTracker();
        tracker.Record(new UsageSample("Fetcher", "claude-sonnet-5", 100, 50));

        var summary = tracker.Summarize();

        Assert.Contains("Fetcher", summary);
        Assert.Contains("claude-sonnet-5", summary);
    }

    [Fact]
    public void Extract_MessageWithNoMetadata_ReturnsNull()
    {
        var message = new ChatMessageContent(AuthorRole.Assistant, "hi");

        var sample = UsageExtractor.Extract("Fetcher", "test-model", message);

        Assert.Null(sample);
    }

    [Fact]
    public void Extract_MessageWithUsageMetadata_ReturnsPopulatedSample()
    {
        var message = new ChatMessageContent(AuthorRole.Assistant, "hi")
        {
            Metadata = new Dictionary<string, object?>
            {
                ["PromptTokenCount"] = 42,
                ["CandidatesTokenCount"] = 7
            }
        };

        var sample = UsageExtractor.Extract("Fetcher", "test-model", message);

        Assert.NotNull(sample);
        Assert.Equal(42, sample.PromptTokens);
        Assert.Equal(7, sample.CompletionTokens);
        Assert.Equal("test-model", sample.ModelId);
    }
}
