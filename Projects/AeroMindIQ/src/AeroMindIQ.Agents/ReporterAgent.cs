#pragma warning disable SKEXP0070 // Gemini connector is experimental
#pragma warning disable SKEXP0001 // Agent framework abstractions are experimental

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

namespace AeroMindIQ.Agents;

public sealed record ReporterResult(string ReportMarkdown, IReadOnlyList<UsageSample> Usage);

/// <summary>
/// Agent C: drafts the root-cause markdown report from the Auditor's anomaly and the
/// Fetcher's query findings. Groundedness is enforced structurally in the prompt — it is
/// instructed to cite only values present in the supplied data. An automated LLM-as-judge
/// groundedness check is a follow-up milestone, not built here.
/// </summary>
public sealed class ReporterAgent
{
    private const string AgentName = "Reporter";
    private readonly ChatCompletionAgent _agent;

    public ReporterAgent(string geminiApiKey, string geminiModelId)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion(modelId: geminiModelId, apiKey: geminiApiKey);
        var kernel = builder.Build();

        _agent = new ChatCompletionAgent
        {
            Name = AgentName,
            Instructions = """
                You are a root-cause reporting agent for a manufacturing operations team.
                You will be given an anomaly summary and the raw query results a data-fetching
                agent gathered. Draft a concise Markdown root-cause report with these sections:

                ## Summary
                ## Evidence
                ## Likely Root Cause
                ## Recommended Action

                Critical rule: cite only numbers and facts that appear in the supplied anomaly
                context or query results. Never invent data. If the evidence is insufficient to
                identify a root cause, say so explicitly in the Likely Root Cause section rather
                than guessing.
                """,
            Kernel = kernel
        };
    }

    public async Task<ReporterResult> DraftReportAsync(AnomalyContext anomaly, string fetcherFindings)
    {
        var thread = new ChatHistoryAgentThread();
        var usage = new List<UsageSample>();
        var reportParts = new List<string>();

        var prompt = $"""
            Anomaly context: {anomaly.Describe()}

            Query findings from the data-fetching agent:
            {fetcherFindings}

            Draft the root-cause report now.
            """;

        await foreach (var response in _agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt), thread))
        {
            var message = response.Message;
            reportParts.Add(message.Content ?? string.Empty);

            var sample = UsageExtractor.Extract(AgentName, message);
            if (sample is not null)
                usage.Add(sample);
        }

        return new ReporterResult(string.Join("\n", reportParts), usage);
    }
}
