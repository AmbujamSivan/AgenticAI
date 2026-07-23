using System.Text.Json.Serialization;

namespace RcaEngine.Models;

/// <summary>High-level failing subsystem categories the triage engine can isolate.</summary>
public enum FailureCategory
{
    MemorySubsystem,
    StorageNvme,
    PcieLink,
    PcieEnumeration,   // device/function fails to enumerate (config space, BARs, FW init)
    DpuOffload,        // DPU/SmartNIC offload engine degraded; datapath fell back to host
    Cpu,
    Power,
    Thermal,
    Firmware,
    Unknown
}

public sealed class EvidenceItem
{
    public required string Source { get; init; }   // e.g. "dmesg", "redfish", "pcie-aer"
    public required string Detail { get; init; }
}

public sealed class RcaReport
{
    public string IncidentId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FailureCategory Category { get; set; } = FailureCategory.Unknown;

    /// <summary>The specific component isolated as faulty, e.g. "DIMM_B0", "0000:3b:00.0 (NVMe)".</summary>
    public string FailingComponent { get; set; } = "";

    /// <summary>0.0 – 1.0 confidence in the verdict.</summary>
    public double Confidence { get; set; }

    public string Summary { get; set; } = "";
    public string RootCause { get; set; } = "";
    public string CustomerImpact { get; set; } = "";
    public List<EvidenceItem> Evidence { get; set; } = [];
    public List<string> RecommendedActions { get; set; } = [];

    /// <summary>"llm-agent" or "deterministic" — which path produced this report.</summary>
    public string GeneratedBy { get; set; } = "";
    public string? ModelId { get; set; }
    public double TriageDurationSeconds { get; set; }
}
