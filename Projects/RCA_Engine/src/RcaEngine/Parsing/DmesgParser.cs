using System.Globalization;
using System.Text.RegularExpressions;

namespace RcaEngine.Parsing;

/// <summary>Kernel-log fault classes the classifier recognizes.</summary>
public enum DmesgCategory
{
    MemoryCorrectedError,     // EDAC corrected (CE) errors
    MemoryUncorrectedError,   // EDAC uncorrected (UE) errors
    MachineCheck,             // MCE events
    NvmeError,                // NVMe controller timeouts / resets / removal
    PcieAer,                  // pcieport AER messages
    IoError,                  // block-layer / filesystem I/O errors
    Thermal,                  // thermal throttling
    Other
}

public sealed class DmesgLine
{
    public double Timestamp { get; init; }        // seconds since boot
    public string Message { get; init; } = "";
    public DmesgCategory Category { get; init; }
    public string Raw { get; init; } = "";
}

/// <summary>Parses `dmesg` output ([seconds.micros] message) and classifies each line.</summary>
public static partial class DmesgParser
{
    [GeneratedRegex(@"^\[\s*(?<ts>\d+\.\d+)\]\s*(?<msg>.*)$")]
    private static partial Regex LineRegex();

    private static readonly (Regex Pattern, DmesgCategory Category)[] Classifiers =
    [
        (new Regex(@"EDAC.*\bUE\b|Uncorrected error|uncorrectable.*memory", RegexOptions.IgnoreCase), DmesgCategory.MemoryUncorrectedError),
        (new Regex(@"EDAC.*\bCE\b|corrected.*memory error|CE memory", RegexOptions.IgnoreCase), DmesgCategory.MemoryCorrectedError),
        (new Regex(@"\bmce\b|Machine check", RegexOptions.IgnoreCase), DmesgCategory.MachineCheck),
        (new Regex(@"\bnvme\b.*(timeout|abort|controller is down|reset|removing|probe failure|I/O error)", RegexOptions.IgnoreCase), DmesgCategory.NvmeError),
        (new Regex(@"pcieport.*AER|PCIe Bus Error|AER:", RegexOptions.IgnoreCase), DmesgCategory.PcieAer),
        (new Regex(@"(blk_update_request|Buffer I/O error|lost async page write|critical (target|medium) error)", RegexOptions.IgnoreCase), DmesgCategory.IoError),
        (new Regex(@"thermal|throttl", RegexOptions.IgnoreCase), DmesgCategory.Thermal),
    ];

    public static IReadOnlyList<DmesgLine> Parse(IEnumerable<string> lines)
    {
        var parsed = new List<DmesgLine>();
        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var match = LineRegex().Match(raw);
            var message = match.Success ? match.Groups["msg"].Value : raw.Trim();
            var timestamp = match.Success
                ? double.Parse(match.Groups["ts"].Value, CultureInfo.InvariantCulture)
                : 0;

            parsed.Add(new DmesgLine
            {
                Timestamp = timestamp,
                Message = message,
                Category = Classify(message),
                Raw = raw
            });
        }
        return parsed;
    }

    public static DmesgCategory Classify(string message)
    {
        foreach (var (pattern, category) in Classifiers)
            if (pattern.IsMatch(message))
                return category;
        return DmesgCategory.Other;
    }
}
