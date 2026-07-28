using System.Text.Json.Serialization;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// A Redfish <c>ComputerSystem</c> — a logical server: its identity, power state,
/// health, aggregate processor/memory summaries, and links to its component
/// collections (Processors, Memory, PCIeDevices).
/// </summary>
public sealed class ComputerSystem : ResourceBase
{
    public string SystemType { get; set; } = "Physical";
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SKU { get; set; }
    public string? UUID { get; set; }
    public string? BiosVersion { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PowerState PowerState { get; set; } = PowerState.On;

    public Status Status { get; set; } = Status.Ok();

    public ProcessorSummary? ProcessorSummary { get; set; }
    public MemorySummary? MemorySummary { get; set; }

    public NavigationLink? Processors { get; set; }
    public NavigationLink? Memory { get; set; }
    public NavigationLink? PCIeDevices { get; set; }

    public ComputerSystemActions? Actions { get; set; }
}

/// <summary>The <c>Actions</c> block of a ComputerSystem (here, OEM actions only).</summary>
public sealed class ComputerSystemActions
{
    public ComputerSystemOemActions? Oem { get; set; }
}

/// <summary>OEM-defined actions on a ComputerSystem.</summary>
public sealed class ComputerSystemOemActions
{
    [JsonPropertyName("#RedfishEmulator.RunDiagnostics")]
    public ActionTarget? RunDiagnostics { get; set; }
}

/// <summary>The invocation target of a Redfish action.</summary>
public sealed class ActionTarget
{
    [JsonPropertyName("target")]
    public required string Target { get; set; }
}

/// <summary>Aggregate view of a system's processors (Redfish <c>ProcessorSummary</c>).</summary>
public sealed class ProcessorSummary
{
    /// <summary>Number of physical processors (CPU sockets).</summary>
    public int Count { get; set; }

    /// <summary>Representative processor model string.</summary>
    public string? Model { get; set; }

    /// <summary>Rolled-up health across all processors.</summary>
    public Status Status { get; set; } = Status.Ok();
}

/// <summary>Aggregate view of a system's memory (Redfish <c>MemorySummary</c>).</summary>
public sealed class MemorySummary
{
    /// <summary>Total installed system memory, in GiB.</summary>
    public double TotalSystemMemoryGiB { get; set; }

    /// <summary>Rolled-up health across all memory modules.</summary>
    public Status Status { get; set; } = Status.Ok();
}
