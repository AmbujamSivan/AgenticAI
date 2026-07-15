#pragma warning disable SKEXP0070 // Gemini connector is experimental
#pragma warning disable SKEXP0001 // Agent framework abstractions are experimental

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

namespace AeroMindIQ.Agents;

public sealed record FetcherResult(string QueryFindings, IReadOnlyList<UsageSample> Usage);

/// <summary>
/// Agent B: given the Auditor's AnomalyContext, writes and executes safe SQL (via
/// DatabasePlugin/SqlGuard) to gather supporting evidence, and summarizes what it found.
/// </summary>
public sealed class FetcherAgent
{
    private const string AgentName = "Fetcher";
    private readonly ChatCompletionAgent _agent;

    public FetcherAgent(string geminiApiKey, string geminiModelId, string readOnlyConnectionString, string schemaDescription)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddGoogleAIGeminiChatCompletion(modelId: geminiModelId, apiKey: geminiApiKey);
        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(new DatabasePlugin(readOnlyConnectionString), "Database");

        _agent = new ChatCompletionAgent
        {
            Name = AgentName,
            Instructions = $"""
                You are a data-fetching agent investigating a production anomaly in a
                manufacturing database. You may only access data via the run_read_only_query
                function, which accepts a single read-only SELECT statement. Never ask for
                anything but SELECT queries — writes are not possible and will be rejected.

                Database schema:
                {schemaDescription}

                Given the anomaly context, write and execute one or more SELECT queries that
                gather the supporting evidence needed to explain the anomaly (surrounding rows,
                comparable shifts/lines, trend over time). Then summarize the raw data you
                found in plain text — do not draw conclusions, that is another agent's job.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(new GeminiPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };
    }

    public async Task<FetcherResult> InvestigateAsync(AnomalyContext anomaly)
    {
        var thread = new ChatHistoryAgentThread();
        var usage = new List<UsageSample>();
        var findings = new List<string>();

        var prompt = $"Anomaly detected: {anomaly.Describe()}\n\nInvestigate and report the raw data you gathered.";

        await foreach (var response in _agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt), thread))
        {
            var message = response.Message;
            findings.Add(message.Content ?? string.Empty);

            var sample = UsageExtractor.Extract(AgentName, message);
            if (sample is not null)
                usage.Add(sample);
        }

        return new FetcherResult(string.Join("\n", findings), usage);
    }
}
