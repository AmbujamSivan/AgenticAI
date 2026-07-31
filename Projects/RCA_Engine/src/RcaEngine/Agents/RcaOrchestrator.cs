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
public sealed partial class RcaOrchestrator(LlmOptions llmOptions, ILogger logger)
{
    private const string SystemPrompt =
        """
        You are a senior datacenter hardware triage engineer. You are given tools that
        expose a node's diagnostic bundle: the Redfish (BMC) system event log, PCIe AER
        error registers, the kernel log (dmesg), the platform's pre-OS boot-progress log
        (POST/UEFI stages), and — when the node has a DPU — the DPU's own control-plane
        console (its internal view, independent of the host).

        Investigate methodically:
        1. Start with the summaries (get_dmesg_summary, get_redfish_event_summary, list_pcie_devices,
           get_boot_progress, get_dpu_console_summary).
        2. Drill into the noisiest subsystem with the detailed tools (search_dmesg,
           get_redfish_events, decode_pcie_aer_registers, search_dpu_console) to isolate
           the failing component. When host-side evidence implicates a DPU, corroborate
           with the DPU's own console before concluding.
        3. Distinguish root cause from symptoms. Corrected memory errors point at a DIMM,
           not the CPU. NVMe controller-down with clean PCIe links points at the drive,
           not the fabric. A correctable AER storm with a downtrained link points at the
           physical link (connector/riser), not the endpoint logic. Config-space read
           failures and firmware-init probe timeouts (missing PCIe functions) point at the
           endpoint device's enumeration/firmware path (PcieEnumeration), not the host.
           Flow-offload programming failures with 'falling back to host datapath' point at
           the DPU/SmartNIC offload engine (DpuOffload) — host CPU pressure is the symptom.

        4. Recommend the fix that targets the DEEPEST root cause you found, not the surface
           symptom — and make sure your recommended actions are consistent with the evidence.
           If a log already shows that a remediation was tried and failed, do not recommend
           it again. Concretely: if the DPU console shows a firmware IMAGE is corrupt (e.g. a
           CRC mismatch) and the device's watchdog already exhausted its recovery retries on
           that same image, then a reboot or power-cycle only reloads the same bad image and
           cannot help — the correct fix is to reflash the firmware or boot the alternate
           image slot. Read the DPU console before recommending a reboot for an enumeration
           failure.

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
        kernel.Plugins.AddFromObject(new BootPlugin(bundle), "boot");
        kernel.Plugins.AddFromObject(new DpuPlugin(bundle), "dpu");

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
                "based only on the evidence the tools returned above. For failingComponent, " +
                "copy the component's address and description exactly as a tool reported them " +
                "- do not re-describe the device from memory.");

            for (var attempt = 0; attempt < 3 && reportPlugin.SubmittedReport is null; attempt++)
            {
                if (attempt > 0)
                    history.AddUserMessage("Your submission was rejected or missing. Call submit_rca_report again with complete, valid arguments.");

                var response = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
                history.Add(response);
                logger.LogDebug("Submission attempt {Attempt} response: {Content}", attempt, response.Content);
            }

            // Cross-check: when the rule-based triage lands on the same category but a
            // different component, challenge the agent once to re-examine its evidence.
            if (reportPlugin.SubmittedReport is { } submitted)
            {
                var crossCheck = DeterministicTriage.Run(bundle);
                if (crossCheck.Category == submitted.Category &&
                    crossCheck.FailingComponent.Length > 0 &&
                    !ComponentsAgree(submitted.FailingComponent, crossCheck.FailingComponent))
                {
                    logger.LogInformation(
                        "Cross-check mismatch: agent named '{Agent}', deterministic triage named '{Det}'. Asking agent to reconcile.",
                        submitted.FailingComponent, crossCheck.FailingComponent);
                    history.AddUserMessage(
                        $"Cross-check: an independent rule-based triage of the same bundle isolated " +
                        $"'{crossCheck.FailingComponent}' as the failing component for the same category, " +
                        $"but your report names '{submitted.FailingComponent}'. Re-examine the tool outputs " +
                        "above. If you misattributed the component, call submit_rca_report again with the " +
                        "corrected component; if you stand by your answer, do nothing further.");
                    var response = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
                    history.Add(response);

                    // Still irreconcilable: the deterministic component is evidence-derived,
                    // so prefer it — and record the correction in the report itself.
                    var final = reportPlugin.SubmittedReport!;
                    if (!ComponentsAgree(final.FailingComponent, crossCheck.FailingComponent))
                    {
                        logger.LogWarning(
                            "Agent kept '{Agent}' after cross-check; overriding component with evidence-derived '{Det}'.",
                            final.FailingComponent, crossCheck.FailingComponent);
                        final.Evidence.Add(new Models.EvidenceItem
                        {
                            Source = "cross-check",
                            Detail = $"Failing component corrected from '{final.FailingComponent}' to '{crossCheck.FailingComponent}' by deterministic cross-check (same category; component derived from parsed evidence)."
                        });
                        final.FailingComponent = crossCheck.FailingComponent;
                    }
                }

                // Remediation safety net: when the categories agree, the deterministic verdict's
                // recommended actions are evidence-derived and category-correct. If the agent's
                // actions share nothing with them (e.g. a weak model recommending a warm reboot
                // when the evidence-backed fix is a firmware reflash), graft the deterministic
                // actions in rather than shipping a remediation the evidence doesn't support.
                // Generic across every failure category.
                if (crossCheck.Category == submitted.Category && crossCheck.RecommendedActions.Count > 0)
                {
                    var final = reportPlugin.SubmittedReport!;
                    if (RemediationDiverges(final.RecommendedActions, crossCheck.RecommendedActions))
                    {
                        logger.LogWarning(
                            "Agent remediation diverges from evidence-derived actions for category {Category}; grafting deterministic actions.",
                            submitted.Category);
                        final.Evidence.Add(new Models.EvidenceItem
                        {
                            Source = "cross-check",
                            Detail = "Agent's recommended actions did not address the evidence-derived root cause; " +
                                     "appended deterministic remediation for this failure category."
                        });
                        foreach (var action in crossCheck.RecommendedActions)
                            if (!final.RecommendedActions.Contains(action))
                                final.RecommendedActions.Add(action);
                    }
                }
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

    // Generic recovery / verification / filler words that appear across unrelated
    // remediations, so sharing one does NOT mean two fixes are actually aligned. Only
    // substantive fix terms (reflash, firmware, reseat, replace, slot, offload, ...) count.
    private static readonly HashSet<string> RemediationStopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "reboot", "reset", "power", "cycle", "warm", "cold",
        "verify", "confirm", "check", "ensure", "collect", "capture", "review",
        "device", "before", "after", "still", "which", "their", "there", "against",
        "should", "would", "these", "those", "until", "while", "reached", "level",
        "known", "based", "using", "again", "rely", "reloads", "corrupt", "image",
        "next", "service", "window", "same", "logs",
    };

    /// <summary>
    /// True when the agent's remediation shares NO substantive fix term with any of the
    /// evidence-derived (deterministic) actions — i.e. the agent is recommending a fix the
    /// evidence doesn't support, so the deterministic actions should be grafted in.
    /// </summary>
    internal static bool RemediationDiverges(IReadOnlyList<string> agentActions, IReadOnlyList<string> deterministicActions)
    {
        var agentText = string.Join(" ", agentActions);
        return !deterministicActions.Any(det => SharesSignificantToken(agentText, det));
    }

    /// <summary>
    /// True when <paramref name="candidate"/> shares at least one substantive (non-stopword,
    /// length ≥ 5) token with <paramref name="agentActions"/> — i.e. the two remediations are
    /// talking about the same kind of fix, not merely both saying "verify" or "reboot".
    /// </summary>
    private static bool SharesSignificantToken(string agentActions, string candidate)
    {
        var haystack = agentActions.ToLowerInvariant();
        foreach (var token in TokenRegex().Matches(candidate).Select(m => m.Value.ToLowerInvariant()))
        {
            if (token.Length < 5 || RemediationStopwords.Contains(token)) continue;
            if (haystack.Contains(token, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[A-Za-z][A-Za-z-]{2,}")]
    private static partial System.Text.RegularExpressions.Regex TokenRegex();

    /// <summary>Two component strings agree when they reference the same PCIe address (or one contains the other).</summary>
    private static bool ComponentsAgree(string a, string b)
    {
        var addrA = PciAddressRegex().Match(a);
        var addrB = PciAddressRegex().Match(b);
        if (addrA.Success && addrB.Success)
            return string.Equals(addrA.Value, addrB.Value, StringComparison.OrdinalIgnoreCase);
        return a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"[0-9a-f]{4}:[0-9a-f]{2}:[0-9a-f]{2}\.\d", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex PciAddressRegex();
}
