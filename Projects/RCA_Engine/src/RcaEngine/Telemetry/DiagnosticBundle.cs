using System.Text.Json;
using RcaEngine.Parsing;

namespace RcaEngine.Telemetry;

/// <summary>Metadata sidecar (bundle.json) describing where/when the bundle was collected.</summary>
public sealed class BundleMetadata
{
    public string IncidentId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
}

/// <summary>
/// A parsed diagnostic bundle: Redfish event log + PCIe AER register dump + kernel dmesg.
/// This is the single source of truth handed to both the agent tools and the deterministic triage.
/// </summary>
public sealed class DiagnosticBundle
{
    public required BundleMetadata Metadata { get; init; }
    public required IReadOnlyList<RedfishEvent> RedfishEvents { get; init; }
    public required IReadOnlyList<PcieDevice> PcieDevices { get; init; }
    public required IReadOnlyList<DmesgLine> DmesgLines { get; init; }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static DiagnosticBundle Load(string bundleDir)
    {
        if (!Directory.Exists(bundleDir))
            throw new DirectoryNotFoundException($"Bundle directory not found: {bundleDir}");

        var metadataPath = Path.Combine(bundleDir, "bundle.json");
        var metadata = File.Exists(metadataPath)
            ? JsonSerializer.Deserialize<BundleMetadata>(File.ReadAllText(metadataPath), JsonOpts) ?? new BundleMetadata()
            : new BundleMetadata { IncidentId = "INC-UNKNOWN", NodeId = Path.GetFileName(bundleDir) };

        var redfishPath = Path.Combine(bundleDir, "redfish_events.json");
        var redfishEvents = File.Exists(redfishPath)
            ? RedfishEventParser.Parse(File.ReadAllText(redfishPath))
            : [];

        var pciePath = Path.Combine(bundleDir, "pcie_aer.txt");
        var pcieDevices = File.Exists(pciePath)
            ? PcieAerParser.Parse(File.ReadAllText(pciePath))
            : [];

        var dmesgPath = Path.Combine(bundleDir, "dmesg.log");
        var dmesgLines = File.Exists(dmesgPath)
            ? DmesgParser.Parse(File.ReadAllLines(dmesgPath))
            : [];

        return new DiagnosticBundle
        {
            Metadata = metadata,
            RedfishEvents = redfishEvents,
            PcieDevices = pcieDevices,
            DmesgLines = dmesgLines
        };
    }
}
