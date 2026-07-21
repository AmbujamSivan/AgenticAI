using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using RcaEngine.Llm;
using RcaEngine.Models;
using RcaEngine.Telemetry;
using RcaEngine.Tools;
using RcaEngine.Triage;

namespace RcaEngine.Agents;

/// <summary>
/// Drives the agentic investigation loop: the LLM plans tool calls against the
/// diagnostic plugins (Redfish, PCIe AER, dmesg) and must finish by submitting a
/// structured report. Falls back to deterministic triage if the model never submits.
/// </summary>
public sealed class RcaOrchestrator(LlmOptions llmOptions, ILogger logger)
{
    private const string SystemPrompt =
        """
        You are a senior datacenter hardware triage engineer. You are given tools that
        expose a node's diagnostic bundle: the Redfish (BMC) system event log, PCIe AER
        error registers, and the kernel log (dmesg).

        Investigate methodically:
        1. Start with the summaries (get_dmesg_summary, get_redfish_event_summary, list_pcie_devices).
        2. Drill into the noisiest subsystem with the detailed tools (search_dmesg,
           get_redfish_events, decode_pcie_aer_registers) to isolate the failing component.
        3. Distinguish root cause from symptoms. Corrected memory errors point at a DIMM,
           not the CPU. NVMe controller-down with clean PCIe links points at the drive,
           not the fabric. A correctable AER storm with a downtrained link points at the
           physical link (connector/riser), not the endpoint logic.

        When you have isolated the failure, call submit_rca_report exactly once with your
        structured verdict, then stop. Keep tool arguments precise. Do not invent evidence:
        cite only what the tools returned.
        """;

    public async Task<RcaReport> RunAsync(DiagnosticBundle bundle, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var (kernel, modelId) = KernelFactory.Create(llmOptions);

        var reportPlugin = new ReportPlugin();
        kernel.Plugins.AddFromObject(new RedfishPlugin(bundle), "redfish");
        kernel.Plugins.AddFromObject(new PciePlugin(bundle), "pcie");
        kernel.Plugins.AddFromObject(new DmesgPlugin(bundle), "dmesg");

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.1,
        };

        var history = new ChatHistory();
        history.AddSystemMessage(SystemPrompt);
        history.AddUserMessage(
            $"Diagnostic bundle from node '{bundle.Metadata.NodeId}' " +
            $"(incident {bundle.Metadata.IncidentId}, collected {bundle.Metadata.CollectedAt:u}). " +
            "Investigate now using the diagnostic tools, then state which subsystem is failing, " +
            "the specific component, and your key evidence.");

        try
        {
            // Phase 1: investigation. The report tool is deliberately absent so the
            // model must gather evidence before it can conclude anything.
            var findings = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
            history.Add(findings);
            logger.LogInformation("Agent findings: {Content}", findings.Content);

            // Phase 2: submission. Now expose submit_rca_report; invalid or empty
            // submissions are rejected by the tool itself, prompting a retry.
            kernel.Plugins.AddFromObject(reportPlugin, "report");
            history.AddUserMessage(
                "Now call submit_rca_report exactly once with your final structured verdict, " +
                "based only on the evidence the tools returned above.");

            for (var attempt = 0; attempt < 3 && reportPlugin.SubmittedReport is null; attempt++)
            {
                if (attempt > 0)
                    history.AddUserMessage("Your submission was rejected or missing. Call submit_rca_report again with complete, valid arguments.");

                var response = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
                history.Add(response);
                logger.LogDebug("Submission attempt {Attempt} response: {Content}", attempt, response.Content);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or KernelException)
        {
            logger.LogWarning(ex, "LLM agent path failed; falling back to deterministic triage.");
        }

        RcaReport report;
        if (reportPlugin.SubmittedReport is not null)
        {
            report = reportPlugin.SubmittedReport;
            report.ModelId = modelId;
            logger.LogInformation("Agent submitted RCA verdict: {Category} / {Component}",
                report.Category, report.FailingComponent);
        }
        else
        {
            logger.LogWarning("Agent did not submit a report; using deterministic triage result.");
            report = DeterministicTriage.Run(bundle);
        }

        stopwatch.Stop();
        report.IncidentId = bundle.Metadata.IncidentId;
        report.NodeId = bundle.Metadata.NodeId;
        report.TriageDurationSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
        return report;
    }
}
