using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Agents;

public sealed record UsageSample(string AgentName, string ModelId, int PromptTokens, int CompletionTokens);

/// <summary>
/// Reads token usage out of a ChatMessageContent's connector metadata — both the Gemini
/// connector and the custom Anthropic connector write the same PromptTokenCount/
/// CandidatesTokenCount keys, so this needs no provider-specific branching. This is a
/// plain in-process log, not real observability — Langfuse/OTel tracing covers that
/// separately. Exists because surprise LLM cost is an explicit concern for this project.
/// </summary>
public static class UsageExtractor
{
    public static UsageSample? Extract(string agentName, string modelId, ChatMessageContent message)
    {
        if (message.Metadata is null)
            return null;

        var prompt = ReadInt(message.Metadata, "PromptTokenCount");
        var completion = ReadInt(message.Metadata, "CandidatesTokenCount");

        return prompt == 0 && completion == 0
            ? null
            : new UsageSample(agentName, modelId, prompt, completion);
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out var value) && value is int i ? i : 0;
}

public sealed class UsageTracker
{
    // Approximate pricing per 1M tokens at time of writing — verify against each
    // provider's current pricing page before relying on this for real budgeting.
    // Gemini: https://ai.google.dev/gemini-api/docs/pricing
    // Claude: https://www.anthropic.com/pricing
    private static readonly Dictionary<string, (decimal InputPerMillion, decimal OutputPerMillion)> PricingTable =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini-2.5-flash"] = (0.30m, 2.50m),
            ["claude-sonnet-5"] = (3.00m, 15.00m),
        };

    private readonly List<UsageSample> _samples = [];
    private readonly HashSet<string> _unpricedModels = [];

    public void Record(UsageSample? sample)
    {
        if (sample is null)
            return;

        _samples.Add(sample);
        if (!PricingTable.ContainsKey(sample.ModelId))
            _unpricedModels.Add(sample.ModelId);
    }

    public int TotalPromptTokens => _samples.Sum(s => s.PromptTokens);
    public int TotalCompletionTokens => _samples.Sum(s => s.CompletionTokens);

    public decimal EstimatedCostUsd =>
        _samples.Sum(s =>
        {
            if (!PricingTable.TryGetValue(s.ModelId, out var pricing))
                return 0m;

            return s.PromptTokens / 1_000_000m * pricing.InputPerMillion +
                   s.CompletionTokens / 1_000_000m * pricing.OutputPerMillion;
        });

    public string Summarize()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Token usage & estimated cost");
        foreach (var group in _samples.GroupBy(s => s.AgentName))
        {
            var modelIds = string.Join(", ", group.Select(s => s.ModelId).Distinct());
            sb.AppendLine($"- **{group.Key}** ({modelIds}): {group.Sum(s => s.PromptTokens)} prompt + {group.Sum(s => s.CompletionTokens)} completion tokens");
        }
        sb.AppendLine($"- **Total**: {TotalPromptTokens} prompt + {TotalCompletionTokens} completion tokens");
        sb.AppendLine($"- **Estimated cost**: ${EstimatedCostUsd:F6} (approximate — based on a hardcoded pricing table, not a live source)");
        if (_unpricedModels.Count > 0)
            sb.AppendLine($"- **Note**: no pricing entry for {string.Join(", ", _unpricedModels)} — cost for those calls is excluded above, not zero-cost.");
        return sb.ToString();
    }
}
