using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using RcaEngine.Parsing;
using RcaEngine.Telemetry;

namespace RcaEngine.Tools;

/// <summary>
/// Agent tool surface over the DPU's own control-plane console log — telemetry from
/// inside the SmartNIC/DPU (ATF/UEFI boot chain, NIC firmware, DOCA flow engine),
/// as opposed to what the host observed.
/// </summary>
public sealed class DpuPlugin(DiagnosticBundle bundle)
{
    [KernelFunction("get_dpu_console_summary")]
    [Description("Returns a summary of the DPU-internal (ARM-side) console log: line counts by subsystem and by fault class (boot/firmware errors vs offload-engine errors). This is the DPU's own view — use it to confirm or refute host-side suspicions about the DPU.")]
    public string GetSummary()
    {
        var lines = bundle.DpuConsole;
        if (lines.Count == 0) return "No DPU console data in this bundle (node may not have a DPU, or its management plane was unreachable).";

        var sb = new StringBuilder();
        sb.AppendLine($"Total DPU console lines: {lines.Count}");
        sb.AppendLine("By fault class:");
        foreach (var group in lines.GroupBy(l => l.Category).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {group.Key}: {group.Count()}");
        sb.AppendLine("By subsystem:");
        foreach (var group in lines.GroupBy(l => l.Subsystem).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {group.Key}: {group.Count()}" +
                          (group.Any(l => l.Category != DpuConsoleCategory.Info) ? $" ({group.Count(l => l.Category != DpuConsoleCategory.Info)} errors)" : ""));
        return sb.ToString();
    }

    [KernelFunction("search_dpu_console")]
    [Description("Searches the DPU-internal console log by regex or substring; returns matching lines with timestamps and subsystem. Returns at most 15 matches. Example patterns: 'firmware', 'crc', 'steering', 'offload'.")]
    public string Search(
        [Description("Regex or substring to search for")] string pattern,
        [Description("Maximum matches to return (default 15)")] int maxMatches = 15)
    {
        if (bundle.DpuConsole.Count == 0) return "No DPU console data in this bundle.";

        Regex regex;
        try { regex = new Regex(pattern, RegexOptions.IgnoreCase); }
        catch (ArgumentException) { regex = new Regex(Regex.Escape(pattern), RegexOptions.IgnoreCase); }

        var matches = bundle.DpuConsole
            .Where(l => regex.IsMatch(l.Message) || regex.IsMatch(l.Subsystem))
            .ToList();
        if (matches.Count == 0) return $"No DPU console lines match '{pattern}'.";

        var sb = new StringBuilder();
        sb.AppendLine($"{matches.Count} lines match '{pattern}'; showing first {Math.Min(matches.Count, Math.Max(1, maxMatches))}:");
        foreach (var line in matches.Take(Math.Max(1, maxMatches)))
            sb.AppendLine($"[{line.Timestamp:HH:mm:ss}] {line.Subsystem}: {line.Message}");
        return sb.ToString();
    }
}
