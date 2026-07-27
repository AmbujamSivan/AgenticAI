using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>A Redfish <c>Memory</c> resource — a single memory module (DIMM).</summary>
public sealed class Memory : ResourceBase
{
    /// <summary>Module capacity in MiB.</summary>
    public int? CapacityMiB { get; set; }

    /// <summary>Memory class, e.g. <c>DRAM</c>.</summary>
    public string? MemoryType { get; set; }

    /// <summary>Device technology, e.g. <c>DDR5</c>.</summary>
    public string? MemoryDeviceType { get; set; }

    public int? OperatingSpeedMhz { get; set; }
    public string? Manufacturer { get; set; }
    public string? PartNumber { get; set; }
    public string? SerialNumber { get; set; }
    public int? DataWidthBits { get; set; }
    public int? BusWidthBits { get; set; }
    public int? RankCount { get; set; }

    public Status Status { get; set; } = Status.Ok();
}
