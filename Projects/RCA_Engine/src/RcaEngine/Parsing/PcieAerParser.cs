using System.Globalization;
using System.Text.RegularExpressions;

namespace RcaEngine.Parsing;

public sealed class PcieLinkState
{
    public string Speed { get; init; } = "";   // e.g. "16GT/s"
    public string Width { get; init; } = "";   // e.g. "x4"
}

public sealed class PcieDevice
{
    public string Address { get; init; } = "";       // e.g. "0000:3b:00.0"
    public string Description { get; init; } = "";
    public PcieLinkState LinkCapabilities { get; init; } = new();
    public PcieLinkState LinkStatus { get; init; } = new();
    public uint AerCorrectableStatus { get; init; }
    public uint AerUncorrectableStatus { get; init; }
    public long AerCorrectableCount { get; init; }

    public bool IsLinkDowntrained =>
        (LinkCapabilities.Speed != "" && LinkStatus.Speed != "" && LinkCapabilities.Speed != LinkStatus.Speed) ||
        (LinkCapabilities.Width != "" && LinkStatus.Width != "" && LinkCapabilities.Width != LinkStatus.Width);

    public IReadOnlyList<string> DecodedCorrectableErrors => PcieAerParser.DecodeCorrectable(AerCorrectableStatus);
    public IReadOnlyList<string> DecodedUncorrectableErrors => PcieAerParser.DecodeUncorrectable(AerUncorrectableStatus);
}

/// <summary>
/// Parses a PCIe AER register dump and decodes status registers into named error bits
/// per PCIe Base Spec §7.8.4 (Uncorrectable Error Status) and §7.8.7 (Correctable Error Status).
/// </summary>
public static partial class PcieAerParser
{
    // PCIe AER Correctable Error Status register bits
    private static readonly (int Bit, string Name)[] CorrectableBits =
    [
        (0,  "ReceiverError"),
        (6,  "BadTLP"),
        (7,  "BadDLLP"),
        (8,  "ReplayNumRollover"),
        (12, "ReplayTimerTimeout"),
        (13, "AdvisoryNonFatalError"),
        (14, "CorrectedInternalError"),
        (15, "HeaderLogOverflow"),
    ];

    // PCIe AER Uncorrectable Error Status register bits
    private static readonly (int Bit, string Name)[] UncorrectableBits =
    [
        (4,  "DataLinkProtocolError"),
        (5,  "SurpriseDownError"),
        (12, "PoisonedTLP"),
        (13, "FlowControlProtocolError"),
        (14, "CompletionTimeout"),
        (15, "CompleterAbort"),
        (16, "UnexpectedCompletion"),
        (17, "ReceiverOverflow"),
        (18, "MalformedTLP"),
        (19, "ECRCError"),
        (20, "UnsupportedRequest"),
        (21, "ACSViolation"),
        (22, "UncorrectableInternalError"),
    ];

    public static IReadOnlyList<string> DecodeCorrectable(uint status) => Decode(status, CorrectableBits);
    public static IReadOnlyList<string> DecodeUncorrectable(uint status) => Decode(status, UncorrectableBits);

    private static List<string> Decode(uint status, (int Bit, string Name)[] table) =>
        table.Where(t => (status & (1u << t.Bit)) != 0).Select(t => t.Name).ToList();

    [GeneratedRegex(@"Speed\s+(?<speed>[\d.]+GT/s)\s*,\s*Width\s+(?<width>x\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    /// <summary>
    /// Parses a device-block register dump. Block format:
    ///   Device: 0000:3b:00.0
    ///   Description: Samsung PM9A3 NVMe SSD Controller
    ///   LinkCapabilities: Speed 16GT/s, Width x4
    ///   LinkStatus: Speed 16GT/s, Width x4
    ///   AerCorrectableStatus: 0x00000000
    ///   AerUncorrectableStatus: 0x00000000
    ///   AerCorrectableCount: 0
    /// </summary>
    public static IReadOnlyList<PcieDevice> Parse(string text)
    {
        var devices = new List<PcieDevice>();
        string? address = null, description = null;
        PcieLinkState linkCap = new(), linkStatus = new();
        uint corrStatus = 0, uncorrStatus = 0;
        long corrCount = 0;

        void Flush()
        {
            if (address is null) return;
            devices.Add(new PcieDevice
            {
                Address = address,
                Description = description ?? "",
                LinkCapabilities = linkCap,
                LinkStatus = linkStatus,
                AerCorrectableStatus = corrStatus,
                AerUncorrectableStatus = uncorrStatus,
                AerCorrectableCount = corrCount
            });
            address = description = null;
            linkCap = new(); linkStatus = new();
            corrStatus = uncorrStatus = 0;
            corrCount = 0;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var sep = line.IndexOf(':');
            if (sep < 0) continue;
            var key = line[..sep].Trim();
            var value = line[(sep + 1)..].Trim();

            switch (key)
            {
                case "Device":
                    Flush();
                    address = value;
                    break;
                case "Description":
                    description = value;
                    break;
                case "LinkCapabilities":
                    linkCap = ParseLink(value);
                    break;
                case "LinkStatus":
                    linkStatus = ParseLink(value);
                    break;
                case "AerCorrectableStatus":
                    corrStatus = ParseHex(value);
                    break;
                case "AerUncorrectableStatus":
                    uncorrStatus = ParseHex(value);
                    break;
                case "AerCorrectableCount":
                    long.TryParse(value, out corrCount);
                    break;
            }
        }
        Flush();
        return devices;
    }

    private static PcieLinkState ParseLink(string value)
    {
        var match = LinkRegex().Match(value);
        return match.Success
            ? new PcieLinkState { Speed = match.Groups["speed"].Value, Width = match.Groups["width"].Value }
            : new PcieLinkState();
    }

    private static uint ParseHex(string value)
    {
        var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
