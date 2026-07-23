using System.Text.RegularExpressions;

namespace RcaEngine.Parsing;

/// <summary>Fault classes recognized in DPU-internal (ARM-side) console logs.</summary>
public enum DpuConsoleCategory
{
    BootError,       // DPU firmware/boot chain failures (ATF, UEFI, NIC subsystem init)
    OffloadError,    // flow/steering engine resource or programming failures
    Info,
}

public sealed class DpuConsoleLine
{
    public DateTimeOffset Timestamp { get; init; }
    public string Subsystem { get; init; } = "";   // e.g. atf, uefi, fw, doca_flow, ovs-vswitchd
    public string Message { get; init; } = "";
    public DpuConsoleCategory Category { get; init; }
}

/// <summary>
/// Parses the DPU's own control-plane console log (management plane, not the host).
/// Line format:  2026-07-21T07:40:05Z uefi: DXE: NIC subsystem firmware load FAILED
/// </summary>
public static partial class DpuConsoleParser
{
    [GeneratedRegex(@"^(?<ts>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)\s+(?<sub>[\w.\-\[\]]+):\s*(?<msg>.*)$")]
    private static partial Regex LineRegex();

    // Offload patterns first: they are more specific, and boot's generic FAILED
    // would otherwise swallow lines like "pipe entry add failed".
    private static readonly (Regex Pattern, DpuConsoleCategory Category)[] Classifiers =
    [
        (new Regex(@"OUT_OF_RESOURCES|out of resources|steering table|STE\b|pipe entry add failed|hw-offload disabled|flow.*(fail|reject)", RegexOptions.IgnoreCase), DpuConsoleCategory.OffloadError),
        (new Regex(@"FAILED|crc mismatch|not ready|gated|watchdog|recovery attempt|HANG|panic", RegexOptions.IgnoreCase), DpuConsoleCategory.BootError),
    ];

    public static IReadOnlyList<DpuConsoleLine> Parse(IEnumerable<string> lines)
    {
        var parsed = new List<DpuConsoleLine>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var match = LineRegex().Match(line);
            if (!match.Success) continue;

            var message = match.Groups["msg"].Value;
            parsed.Add(new DpuConsoleLine
            {
                Timestamp = DateTimeOffset.Parse(match.Groups["ts"].Value),
                Subsystem = match.Groups["sub"].Value,
                Message = message,
                Category = Classify(message)
            });
        }
        return parsed;
    }

    public static DpuConsoleCategory Classify(string message)
    {
        foreach (var (pattern, category) in Classifiers)
            if (pattern.IsMatch(message))
                return category;
        return DpuConsoleCategory.Info;
    }
}
