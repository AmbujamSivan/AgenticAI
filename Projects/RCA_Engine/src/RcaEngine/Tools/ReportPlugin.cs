using System.ComponentModel;
using Microsoft.SemanticKernel;
using RcaEngine.Models;

namespace RcaEngine.Tools;

/// <summary>
/// Terminal tool: the agent submits its final structured verdict here.
/// The captured report is the engine's primary output.
/// </summary>
public sealed class ReportPlugin
{
    public RcaReport? SubmittedReport { get; private set; }

    [KernelFunction("submit_rca_report")]
    [Description("Submits the final root-cause analysis verdict. Call this exactly once, after you have gathered evidence from the diagnostic tools. This ends the investigation.")]
    public string SubmitReport(
        [Description("Failing subsystem. One of: MemorySubsystem, StorageNvme, PcieLink, Cpu, Power, Thermal, Firmware, Unknown")] string category,
        [Description("The specific failing component, e.g. 'DIMM_B0' or '0000:3b:00.0 (NVMe controller)'")] string failingComponent,
        [Description("One-paragraph summary of the incident")] string summary,
        [Description("The root cause, stated precisely")] string rootCause,
        [Description("Impact on the customer workload")] string customerImpact,
        [Description("Recommended remediation actions, separated by ';'")] string recommendedActions,
        [Description("Key evidence supporting the verdict, separated by ';'")] string keyEvidence,
        [Description("Confidence in the verdict from 0.0 to 1.0")] double confidence)
    {
        if (!Enum.TryParse<FailureCategory>(category.Trim(), ignoreCase: true, out var parsedCategory))
            return $"REJECTED: '{category}' is not a valid category. Use one of: MemorySubsystem, StorageNvme, PcieLink, Cpu, Power, Thermal, Firmware. Call submit_rca_report again with corrected arguments.";

        if (string.IsNullOrWhiteSpace(failingComponent) || string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(rootCause))
            return "REJECTED: failingComponent, summary, and rootCause must all be non-empty. Investigate with the diagnostic tools first, then call submit_rca_report again with complete arguments.";

        SubmittedReport = new RcaReport
        {
            Category = parsedCategory,
            FailingComponent = failingComponent.Trim(),
            Summary = summary.Trim(),
            RootCause = rootCause.Trim(),
            CustomerImpact = customerImpact.Trim(),
            RecommendedActions = Split(recommendedActions),
            Evidence = Split(keyEvidence)
                .Select(e => new EvidenceItem { Source = "agent", Detail = e })
                .ToList(),
            Confidence = Math.Clamp(confidence, 0.0, 1.0),
            GeneratedBy = "llm-agent"
        };
        return "RCA report submitted successfully. Investigation complete.";
    }

    private static List<string> Split(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
