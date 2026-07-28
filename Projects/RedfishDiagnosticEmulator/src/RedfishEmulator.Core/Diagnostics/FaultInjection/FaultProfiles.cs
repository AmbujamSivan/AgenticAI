using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.State;

namespace RedfishEmulator.Core.Diagnostics.FaultInjection;

/// <summary>
/// A GPU accelerator stops responding on the PCIe bus — the classic "GPU fell off
/// the bus" failure. The device goes offline and diagnostics fail it.
/// </summary>
public sealed class GpuOffBusFault : FaultProfile
{
    public const string MessageId = "RedfishEmulator.1.0.GpuOffBus";

    public override string Id => "GpuOffBus";
    public override string Name => "GPU Fell Off the Bus";
    public override string Description => "A GPU accelerator stops responding on the PCIe bus and goes offline.";
    public override string TargetKind => "GPU";

    public override string? Apply(IComponentRepository repository) =>
        Fault(
            repository.Processors.FirstOrDefault(p => p.ProcessorType == ProcessorType.GPU),
            Health.Critical, ResourceState.UnavailableOffline, MessageId,
            "GPU has fallen off the PCIe bus; the device is not responding.");
}

/// <summary>A PCIe device fails link training and its link drops to zero lanes.</summary>
public sealed class PcieLinkDownFault : FaultProfile
{
    public override string Id => "PcieLinkDown";
    public override string Name => "PCIe Link Down";
    public override string Description => "A PCIe device fails link training; its link drops offline.";
    public override string TargetKind => "PCIe";

    public override string? Apply(IComponentRepository repository) =>
        Fault(
            repository.PCIeDevices.FirstOrDefault(),
            Health.Critical, ResourceState.UnavailableOffline, "RedfishEmulator.1.0.PcieLinkDown",
            "PCIe link training failed; the device link is down (0 lanes active).");
}

/// <summary>A memory module exceeds its correctable-ECC error threshold (degraded, still online).</summary>
public sealed class MemoryEccFault : FaultProfile
{
    public override string Id => "MemoryEcc";
    public override string Name => "Memory ECC Errors";
    public override string Description => "A DIMM exceeds its correctable ECC error-rate threshold and is degraded.";
    public override string TargetKind => "Memory";

    public override string? Apply(IComponentRepository repository) =>
        Fault(
            repository.MemoryModules.FirstOrDefault(),
            Health.Warning, ResourceState.Enabled, "RedfishEmulator.1.0.MemoryEcc",
            "Correctable ECC error rate has exceeded the warning threshold on this module.");
}

/// <summary>
/// A CPU exceeds its critical temperature threshold and thermal-trips. Also raises the
/// processor's telemetry temperature so the thermal sub-resource reflects the event.
/// </summary>
public sealed class ThermalTripFault : FaultProfile
{
    public const string MessageId = "RedfishEmulator.1.0.ThermalTrip";

    public override string Id => "ThermalTrip";
    public override string Name => "CPU Thermal Trip";
    public override string Description => "A CPU temperature exceeds the critical threshold and thermal-trips.";
    public override string TargetKind => "CPU";

    public override string? Apply(IComponentRepository repository) =>
        Fault(
            repository.Processors.FirstOrDefault(p => p.ProcessorType == ProcessorType.CPU),
            Health.Critical, ResourceState.Enabled, MessageId,
            "Thermal trip: processor temperature exceeded the critical threshold.");
}
