using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using RcaEngine.Telemetry;

namespace RcaEngine.Tools;

/// <summary>Agent tool surface over the Redfish system event log.</summary>
public sealed class RedfishPlugin(DiagnosticBundle bundle)
{
    [KernelFunction("get_redfish_event_summary")]
    [Description("Returns a summary of the Redfish system event log: total events and counts grouped by severity and MessageId. Call this first to see what the BMC recorded.")]
    public string GetEventSummary()
    {
        var events = bundle.RedfishEvents;
        if (events.Count == 0) return "No Redfish events in this bundle.";

        var sb = new StringBuilder();
        sb.AppendLine($"Total Redfish events: {events.Count}");
        sb.AppendLine("By severity:");
        foreach (var group in events.GroupBy(e => e.Severity).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {group.Key}: {group.Count()}");
        sb.AppendLine("By MessageId:");
        foreach (var group in events.GroupBy(e => e.MessageId).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {group.Key}: {group.Count()} (components: {string.Join(", ", group.Select(e => e.OriginOfCondition).Distinct().Take(4))})");
        return sb.ToString();
    }

    [KernelFunction("get_redfish_events")]
    [Description("Returns individual Redfish events, optionally filtered by severity (Warning, Critical) or by a substring of the component path (e.g. 'DIMM', 'Drive'). Returns at most 20 events.")]
    public string GetEvents(
        [Description("Optional severity filter: OK, Warning, or Critical")] string? severity = null,
        [Description("Optional substring to match against the component path")] string? component = null)
    {
        var filtered = bundle.RedfishEvents.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(severity))
            filtered = filtered.Where(e => e.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(component))
            filtered = filtered.Where(e => e.OriginOfCondition.Contains(component, StringComparison.OrdinalIgnoreCase));

        var results = filtered.Take(20).ToList();
        if (results.Count == 0) return "No matching Redfish events.";

        var sb = new StringBuilder();
        foreach (var e in results)
            sb.AppendLine($"[{e.Created:u}] {e.Severity} {e.MessageId} @ {e.OriginOfCondition}: {e.Message}");
        return sb.ToString();
    }
}
