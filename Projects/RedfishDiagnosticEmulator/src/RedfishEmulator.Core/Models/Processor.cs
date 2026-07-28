using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// A Redfish <c>Processor</c> — a CPU socket or a GPU/accelerator. The same
/// resource type covers both; <see cref="ProcessorType"/> distinguishes them
/// (<c>CPU</c> vs <c>GPU</c>), which is exactly how a datacenter BMC inventories
/// host CPUs alongside GPU accelerators.
/// </summary>
public sealed class Processor : ResourceBase, IDiagnosable
{
    /// <summary>Physical socket/slot label, e.g. <c>CPU 1</c> or <c>GPU 3</c>.</summary>
    public string? Socket { get; set; }

    public ProcessorType ProcessorType { get; set; } = ProcessorType.CPU;

    /// <summary>Architecture, e.g. <c>x86-64</c> for CPUs or <c>GPU</c> for accelerators.</summary>
    public string? ProcessorArchitecture { get; set; }

    public string? InstructionSet { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public int? MaxSpeedMHz { get; set; }
    public int? TotalCores { get; set; }
    public int? TotalThreads { get; set; }

    public Status Status { get; set; } = Status.Ok();
}
