using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Agents;

public sealed record UsageSample(string AgentName, int PromptTokens, int CompletionTokens);

/// <summary>
/// Reads token usage out of a Gemini ChatMessageContent's connector metadata. This is a
/// plain in-process log, not real observability — Langfuse/OTel tracing is a follow-up
/// milestone. Exists because surprise LLM cost is an explicit concern for this project.
/// </summary>
public static class UsageExtractor
{
    public static UsageSample? Extract(string agentName, ChatMessageContent message)
    {
        if (message.Metadata is null)
            return null;

        var prompt = ReadInt(message.Metadata, "PromptTokenCount");
        var completion = ReadInt(message.Metadata, "CandidatesTokenCount");

        return prompt == 0 && completion == 0
            ? null
            : new UsageSample(agentName, prompt, completion);
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && value is int i ? i : 0;
}

public sealed class UsageTracker
{
    // Approximate Gemini 2.5 Flash pricing at time of writing — verify against
    // https://ai.google.dev/gemini-api/docs/pricing before relying on this for real budgeting.
    private const decimal InputPricePerMillionTokens = 0.30m;
    private const decimal OutputPricePerMillionTokens = 2.50m;

    private readonly List<UsageSample> _samples = [];

    public void Record(UsageSample? sample)
    {
        if (sample is not null)
            _samples.Add(sample);
    }

    public int TotalPromptTokens => _samples.Sum(s => s.PromptTokens);
    public int TotalCompletionTokens => _samples.Sum(s => s.CompletionTokens);

    public decimal EstimatedCostUsd =>
        _samples.Sum(s =>
            s.PromptTokens / 1_000_000m * InputPricePerMillionTokens +
            s.CompletionTokens / 1_000_000m * OutputPricePerMillionTokens);

    public string Summarize()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Token usage & estimated cost");
        foreach (var group in _samples.GroupBy(s => s.AgentName))
        {
            sb.AppendLine($"- **{group.Key}**: {group.Sum(s => s.PromptTokens)} prompt + {group.Sum(s => s.CompletionTokens)} completion tokens");
        }
        sb.AppendLine($"- **Total**: {TotalPromptTokens} prompt + {TotalCompletionTokens} completion tokens");
        sb.AppendLine($"- **Estimated cost**: ${EstimatedCostUsd:F6} (approximate — based on a hardcoded pricing table, not a live source)");
        return sb.ToString();
    }
}
