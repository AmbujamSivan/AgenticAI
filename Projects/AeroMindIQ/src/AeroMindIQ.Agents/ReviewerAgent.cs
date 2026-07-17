#pragma warning disable SKEXP0001 // Agent framework abstractions are experimental

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Agents;

public sealed record ReviewVerdict(bool Approved, string Feedback, UsageSample? Usage);

/// <summary>
/// Reviews the Fetcher's proposed SQL before it runs: checks for logic errors (wrong
/// table/column, a WHERE clause that can't plausibly relate to the anomaly) and context
/// leaks (unbounded SELECT * on unrelated tables). Called from inside DatabasePlugin
/// rather than as a separate pipeline stage, so a rejection reuses the same retry path
/// the Fetcher already uses for SqlGuardException.
/// </summary>
public sealed class ReviewerAgent(LlmProviderConfig providerConfig)
{
    private const string AgentName = "Reviewer";

    public Task<ReviewVerdict> ReviewAsync(string sql, string schemaDescription, AnomalyContext anomaly) =>
        LlmRetryPolicy.ExecuteAsync(() => ReviewOnceAsync(sql, schemaDescription, anomaly), AgentName);

    private async Task<ReviewVerdict> ReviewOnceAsync(string sql, string schemaDescription, AnomalyContext anomaly)
    {
        var kernel = LlmKernelFactory.CreateKernel(providerConfig, providerConfig.ReviewerModel);

        var agent = new ChatCompletionAgent
        {
            Name = AgentName,
            Instructions = """
                You are a SQL reviewer for a production anomaly investigation. You will be
                given the database schema, the anomaly being investigated, and a proposed
                SELECT statement. Check for:
                - Logic errors: wrong table/column names, a WHERE clause that cannot
                  plausibly relate to the anomaly (wrong line_id, wrong time window).
                - Context leaks: unbounded SELECT * on tables unrelated to the anomaly,
                  pulling far more data than needed to investigate it.

                Respond in exactly this format:
                First line: either APPROVED or REJECTED
                Remaining lines: brief feedback (if REJECTED, explain exactly what to fix)
                """,
            Kernel = kernel
        };

        var thread = new ChatHistoryAgentThread();
        var prompt = $"""
            Anomaly under investigation: {anomaly.Describe()}

            Database schema:
            {schemaDescription}

            Proposed SQL:
            {sql}

            Review it now.
            """;

        ChatMessageContent? lastMessage = null;
        UsageSample? usage = null;

        await foreach (var response in agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt), thread))
        {
            lastMessage = response.Message;
            usage = UsageExtractor.Extract(AgentName, providerConfig.ReviewerModel, response.Message) ?? usage;
        }

        var content = lastMessage?.Content?.Trim() ?? string.Empty;
        var approved = content.StartsWith("APPROVED", StringComparison.OrdinalIgnoreCase);

        return new ReviewVerdict(approved, content, usage);
    }
}
