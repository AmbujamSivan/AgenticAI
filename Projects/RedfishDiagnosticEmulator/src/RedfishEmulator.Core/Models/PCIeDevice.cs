using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>A Redfish <c>PCIeDevice</c> — an installed PCI Express device (NIC, NVMe, GPU bridge).</summary>
public sealed class PCIeDevice : ResourceBase
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    /// <summary>Device type, e.g. <c>SingleFunction</c> or <c>MultiFunction</c>.</summary>
    public string? DeviceType { get; set; }

    public PCIeInterface? PCIeInterface { get; set; }

    public Status Status { get; set; } = Status.Ok();
}

/// <summary>The negotiated PCIe link of a device (Redfish <c>PCIeInterface</c>).</summary>
public sealed class PCIeInterface
{
    /// <summary>Lanes currently in use, e.g. 16.</summary>
    public int? LanesInUse { get; set; }

    /// <summary>Maximum lanes the slot supports.</summary>
    public int? MaxLanes { get; set; }

    /// <summary>Generation currently negotiated, e.g. <c>Gen5</c>.</summary>
    public string? PCIeType { get; set; }

    /// <summary>Maximum generation supported, e.g. <c>Gen5</c>.</summary>
    public string? MaxPCIeType { get; set; }
}
