using AeroMindIQ.Agents;
using AeroMindIQ.Data;

namespace AeroMindIQ.Tests;

/// <summary>
/// Opt-in tests that hit the real Claude API and the local Postgres from
/// db/docker-compose.yml. Excluded from the default `dotnet test` run — use
/// `dotnet test --filter Category=Live` to include them, and set ANTHROPIC_API_KEY in
/// the environment first. Costs real tokens each run, so this is intentionally the one
/// test class in the suite that isn't free — everything else (including the connector's
/// tool-calling loop itself, in AnthropicChatCompletionServiceTests) is covered by mocks.
/// </summary>
[Trait("Category", "Live")]
public class FetcherAgentLiveTests
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=aeromindiq;Username=aeromind_admin;Password=aeromind_admin_pw";
    private const string ReadOnlyConnectionString =
        "Host=localhost;Port=5432;Database=aeromindiq;Username=aeromind_reader;Password=aeromind_reader_pw";

    [Fact]
    public async Task InvestigateAsync_RealClaude_ExecutesAtLeastOneQueryAsync()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("Skipping: ANTHROPIC_API_KEY not set in environment.");
            return;
        }

        var providerConfig = new LlmProviderConfig("Claude", apiKey, null, "claude-sonnet-5", "claude-sonnet-5", "claude-sonnet-5", "claude-sonnet-5");
        var schemaDescription = await SchemaReader.DescribeSchemaAsync(AdminConnectionString);

        var reviewerChat = LlmKernelFactory.CreateChatCompletionService(providerConfig, providerConfig.ReviewerModel);
        var reviewer = new ReviewerAgent(reviewerChat, providerConfig.ReviewerModel);

        var fetcherChat = LlmKernelFactory.CreateChatCompletionService(providerConfig, providerConfig.FetcherModel);
        var fetcher = new FetcherAgent(fetcherChat, providerConfig.FetcherModel, ReadOnlyConnectionString, schemaDescription, reviewer);

        var anomaly = new AnomalyContext(
            LineId: 2,
            WindowStart: DateTime.UtcNow.AddHours(-1),
            WindowEnd: DateTime.UtcNow,
            ObservedYieldPct: 68m,
            BaselineMeanYieldPct: 97m,
            BaselineStdDevYieldPct: 2m,
            ZScore: 12m);

        var result = await fetcher.InvestigateAsync(anomaly);

        Assert.False(string.IsNullOrWhiteSpace(result.QueryFindings));
        Assert.NotEmpty(result.Usage);
    }
}
