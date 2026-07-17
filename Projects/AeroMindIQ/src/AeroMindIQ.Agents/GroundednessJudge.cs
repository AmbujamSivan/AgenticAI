#pragma warning disable SKEXP0001 // Agent framework abstractions are experimental

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Agents;

public sealed record JudgeVerdict(bool Grounded, IReadOnlyList<string> UnsupportedClaims, UsageSample? Usage)
{
    public string Summarize()
    {
        const string caveat = "_(Unvalidated judge — no labeled calibration eval set has been built for " +
                               "this yet; treat this as a first-pass signal, not a guarantee.)_";

        if (Grounded)
            return $"### Groundedness check\n- **GROUNDED** — no unsupported claims found by the judge.\n{caveat}";

        var claims = string.Join("\n", UnsupportedClaims.Select(c => $"  - {c}"));
        return $"### Groundedness check\n- **NOT GROUNDED** — the judge flagged claims not supported by " +
               $"the fetched data:\n{claims}\n{caveat}";
    }
}

/// <summary>
/// LLM-as-judge groundedness check: grades whether the Reporter's final report only
/// cites facts actually present in the Fetcher's query findings. Runs synchronously at
/// the end of each cycle — this is a single-shot console app, not a service with an
/// async eval queue. No labeled eval set calibrates the judge itself yet; that's
/// surfaced explicitly in the output rather than silently treated as validated.
/// </summary>
public sealed class GroundednessJudge(LlmProviderConfig providerConfig)
{
    private const string AgentName = "Judge";

    public Task<JudgeVerdict> EvaluateAsync(string reportMarkdown, string fetcherFindings) =>
        LlmRetryPolicy.ExecuteAsync(() => EvaluateOnceAsync(reportMarkdown, fetcherFindings), AgentName);

    private async Task<JudgeVerdict> EvaluateOnceAsync(string reportMarkdown, string fetcherFindings)
    {
        var kernel = LlmKernelFactory.CreateKernel(providerConfig, providerConfig.JudgeModel);

        var agent = new ChatCompletionAgent
        {
            Name = AgentName,
            Instructions = """
                You are a groundedness judge for a root-cause report. You will be given the
                report and the raw query findings it was based on. Check every factual claim
                (numbers, trends, comparisons) in the report against the findings.

                Respond in exactly this format:
                First line: either GROUNDED or NOT_GROUNDED
                If NOT_GROUNDED, list each unsupported claim on its own line starting with "- "
                Do not flag reasonable interpretation or root-cause inference as unsupported —
                only flag claims that state a specific fact/number not present in the findings.
                """,
            Kernel = kernel
        };

        var thread = new ChatHistoryAgentThread();
        var prompt = $"""
            Report:
            {reportMarkdown}

            Query findings the report was based on:
            {fetcherFindings}

            Evaluate it now.
            """;

        ChatMessageContent? lastMessage = null;
        UsageSample? usage = null;

        await foreach (var response in agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt), thread))
        {
            lastMessage = response.Message;
            usage = UsageExtractor.Extract(AgentName, providerConfig.JudgeModel, response.Message) ?? usage;
        }

        var content = lastMessage?.Content?.Trim() ?? string.Empty;
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var grounded = lines.Length == 0 || lines[0].StartsWith("GROUNDED", StringComparison.OrdinalIgnoreCase);

        var unsupportedClaims = lines
            .Skip(1)
            .Where(l => l.StartsWith('-'))
            .Select(l => l.TrimStart('-', ' '))
            .ToList();

        return new JudgeVerdict(grounded, unsupportedClaims, usage);
    }
}
