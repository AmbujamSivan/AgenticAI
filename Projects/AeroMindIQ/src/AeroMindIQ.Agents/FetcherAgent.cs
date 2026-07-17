#pragma warning disable SKEXP0001 // Agent framework abstractions are experimental

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Agents;

public sealed record FetcherResult(string QueryFindings, IReadOnlyList<UsageSample> Usage);

/// <summary>
/// Agent B: given the Auditor's AnomalyContext, writes and executes safe SQL (via
/// DatabasePlugin, reviewed by the Reviewer agent, guarded by SqlGuard) to gather
/// supporting evidence, and summarizes what it found.
///
/// The kernel/agent/plugin are built fresh inside InvestigateAsync rather than in the
/// constructor, because DatabasePlugin needs the AnomalyContext (only known once an
/// investigation starts) and its rejection counter/usage log should be scoped to one
/// investigation, not the FetcherAgent's whole lifetime.
/// </summary>
public sealed class FetcherAgent(
    LlmProviderConfig providerConfig,
    string readOnlyConnectionString,
    string schemaDescription,
    ReviewerAgent reviewer)
{
    private const string AgentName = "Fetcher";

    public Task<FetcherResult> InvestigateAsync(AnomalyContext anomaly) =>
        LlmRetryPolicy.ExecuteAsync(() => InvestigateOnceAsync(anomaly), AgentName);

    private async Task<FetcherResult> InvestigateOnceAsync(AnomalyContext anomaly)
    {
        var kernel = LlmKernelFactory.CreateKernel(providerConfig, providerConfig.FetcherModel);

        var databasePlugin = new DatabasePlugin(readOnlyConnectionString, reviewer, anomaly, schemaDescription);
        kernel.Plugins.AddFromObject(databasePlugin, "Database");

        var agent = new ChatCompletionAgent
        {
            Name = AgentName,
            Instructions = $"""
                You are a data-fetching agent investigating a production anomaly in a
                manufacturing database. You may only access data via the run_read_only_query
                function, which accepts a single read-only SELECT statement. Every query you
                propose is reviewed by a critic agent before it runs — if it comes back
                rejected, revise it based on the feedback. Never ask for anything but SELECT
                queries — writes are not possible and will be rejected.

                Database schema:
                {schemaDescription}

                Given the anomaly context, write and execute one or more SELECT queries that
                gather the supporting evidence needed to explain the anomaly (surrounding rows,
                comparable shifts/lines, trend over time). Then summarize the raw data you
                found in plain text — do not draw conclusions, that is another agent's job.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            })
        };

        var thread = new ChatHistoryAgentThread();
        var usage = new List<UsageSample>();
        var findings = new List<string>();

        var prompt = $"Anomaly detected: {anomaly.Describe()}\n\nInvestigate and report the raw data you gathered.";

        await foreach (var response in agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt), thread))
        {
            var message = response.Message;
            findings.Add(message.Content ?? string.Empty);

            var sample = UsageExtractor.Extract(AgentName, providerConfig.FetcherModel, message);
            if (sample is not null)
                usage.Add(sample);
        }

        usage.AddRange(databasePlugin.Usage);

        return new FetcherResult(string.Join("\n", findings), usage);
    }
}
