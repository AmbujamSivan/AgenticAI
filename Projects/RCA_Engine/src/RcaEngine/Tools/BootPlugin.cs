using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using RcaEngine.Parsing;
using RcaEngine.Telemetry;

namespace RcaEngine.Tools;

/// <summary>Agent tool surface over the platform boot-progress log (pre-OS POST/UEFI stages).</summary>
public sealed class BootPlugin(DiagnosticBundle bundle)
{
    [KernelFunction("get_boot_progress")]
    [Description("Returns the platform's pre-OS boot flow as captured by the BMC: POST codes across SEC/PEI/DXE/BDS stages, any stage errors (option ROM failures, resource allocation warnings, hangs), and whether the platform reached OS handoff. Use this to check for firmware/boot-stage faults before the OS was even up.")]
    public string GetBootProgress()
    {
        var entries = bundle.BootProgress;
        if (entries.Count == 0) return "No boot-progress data in this bundle.";

        var sb = new StringBuilder();
        var errors = entries.Where(e => e.IsError).ToList();
        sb.AppendLine($"Boot-progress entries: {entries.Count}; stage errors: {errors.Count}; " +
                      $"OS handoff reached: {(BootProgressParser.ReachedOsHandoff(entries) ? "yes" : "NO — boot did not complete")}");
        foreach (var stage in entries.GroupBy(e => e.Stage))
            sb.AppendLine($"  {stage.Key}: {stage.Count()} entries" +
                          (stage.Any(e => e.IsError) ? $" ({stage.Count(e => e.IsError)} with errors)" : ""));
        if (errors.Count > 0)
        {
            sb.AppendLine("Stage errors:");
            foreach (var e in errors.Take(10))
                sb.AppendLine($"  POST 0x{e.Code:X2} {e.Stage}: {e.Message}");
        }
        return sb.ToString();
    }
}
