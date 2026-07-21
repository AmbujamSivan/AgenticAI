using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using RcaEngine.Parsing;
using RcaEngine.Telemetry;

namespace RcaEngine.Tools;

/// <summary>Agent tool surface over the kernel log (dmesg).</summary>
public sealed class DmesgPlugin(DiagnosticBundle bundle)
{
    [KernelFunction("get_dmesg_summary")]
    [Description("Returns kernel log line counts grouped by fault class (memory corrected errors, NVMe errors, PCIe AER, machine checks, I/O errors, thermal). Call this first to see which subsystems are noisy.")]
    public string GetSummary()
    {
        if (bundle.DmesgLines.Count == 0) return "No dmesg data in this bundle.";

        var sb = new StringBuilder();
        sb.AppendLine($"Total kernel log lines: {bundle.DmesgLines.Count}");
        foreach (var group in bundle.DmesgLines
                     .GroupBy(l => l.Category)
                     .Where(g => g.Key != DmesgCategory.Other)
                     .OrderByDescending(g => g.Count()))
        {
            var span = group.Max(l => l.Timestamp) - group.Min(l => l.Timestamp);
            sb.AppendLine($"  {group.Key}: {group.Count()} lines over {span:F0}s");
        }
        var other = bundle.DmesgLines.Count(l => l.Category == DmesgCategory.Other);
        sb.AppendLine($"  (uncategorized/normal: {other})");
        return sb.ToString();
    }

    [KernelFunction("search_dmesg")]
    [Description("Searches kernel log lines by regular expression or plain substring and returns matching lines with timestamps. Returns at most 15 matches. Example patterns: 'EDAC', 'nvme', 'AER', 'DIMM'.")]
    public string Search(
        [Description("Regex or substring to search for")] string pattern,
        [Description("Maximum matches to return (default 15)")] int maxMatches = 15)
    {
        Regex regex;
        try { regex = new Regex(pattern, RegexOptions.IgnoreCase); }
        catch (ArgumentException) { regex = new Regex(Regex.Escape(pattern), RegexOptions.IgnoreCase); }

        var matches = bundle.DmesgLines.Where(l => regex.IsMatch(l.Message)).ToList();
        if (matches.Count == 0) return $"No kernel log lines match '{pattern}'.";

        var sb = new StringBuilder();
        sb.AppendLine($"{matches.Count} lines match '{pattern}'; showing first {Math.Min(matches.Count, Math.Max(1, maxMatches))}:");
        foreach (var line in matches.Take(Math.Max(1, maxMatches)))
            sb.AppendLine($"[{line.Timestamp,12:F6}] {line.Message}");
        return sb.ToString();
    }
}
