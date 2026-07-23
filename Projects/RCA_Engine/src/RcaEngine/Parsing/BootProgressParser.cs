using System.Globalization;
using System.Text.RegularExpressions;

namespace RcaEngine.Parsing;

/// <summary>UEFI platform boot stages, in order.</summary>
public enum BootStage { Sec, Pei, Dxe, Bds, Os, Unknown }

public sealed class BootProgressEntry
{
    public byte Code { get; init; }              // port-80 style POST code
    public BootStage Stage { get; init; }
    public string Message { get; init; } = "";
    public bool IsError { get; init; }
}

/// <summary>
/// Parses a BMC-captured platform boot-progress log (POST codes + UEFI stage messages).
/// Line format:  POST 0x9A DXE: PCI bus enumeration
/// </summary>
public static partial class BootProgressParser
{
    [GeneratedRegex(@"^POST\s+0x(?<code>[0-9A-Fa-f]{2})\s+(?<stage>SEC|PEI|DXE|BDS|OS)\s*:\s*(?<msg>.*)$")]
    private static partial Regex LineRegex();

    private static readonly Regex ErrorRegex = new(@"\b(WARN|FAIL|FAILED|ERROR|HANG|TIMEOUT)\b", RegexOptions.IgnoreCase);

    public static IReadOnlyList<BootProgressEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<BootProgressEntry>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var match = LineRegex().Match(line);
            if (!match.Success) continue;

            var message = match.Groups["msg"].Value;
            entries.Add(new BootProgressEntry
            {
                Code = byte.Parse(match.Groups["code"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                Stage = match.Groups["stage"].Value.ToUpperInvariant() switch
                {
                    "SEC" => BootStage.Sec,
                    "PEI" => BootStage.Pei,
                    "DXE" => BootStage.Dxe,
                    "BDS" => BootStage.Bds,
                    "OS" => BootStage.Os,
                    _ => BootStage.Unknown
                },
                Message = message,
                IsError = ErrorRegex.IsMatch(message)
            });
        }
        return entries;
    }

    /// <summary>True when the log shows the platform handing off to the OS loader.</summary>
    public static bool ReachedOsHandoff(IReadOnlyList<BootProgressEntry> entries) =>
        entries.Any(e => e.Stage == BootStage.Os);
}
