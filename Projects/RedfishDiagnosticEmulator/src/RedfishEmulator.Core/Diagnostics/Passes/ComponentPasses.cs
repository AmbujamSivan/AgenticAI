using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.State;

namespace RedfishEmulator.Core.Diagnostics.Passes;

/// <summary>Diagnoses the host CPUs.</summary>
public sealed class CpuDiagnosticPass : ComponentPass
{
    public override string Category => "CPU";

    protected override IEnumerable<IDiagnosable> Select(IComponentRepository repository) =>
        repository.Processors.Where(p => p.ProcessorType == ProcessorType.CPU);
}

/// <summary>Diagnoses the GPU accelerators.</summary>
public sealed class GpuDiagnosticPass : ComponentPass
{
    public override string Category => "GPU Accelerator";

    protected override IEnumerable<IDiagnosable> Select(IComponentRepository repository) =>
        repository.Processors.Where(p => p.ProcessorType == ProcessorType.GPU);
}

/// <summary>Diagnoses the memory modules.</summary>
public sealed class MemoryDiagnosticPass : ComponentPass
{
    public override string Category => "Memory";

    protected override IEnumerable<IDiagnosable> Select(IComponentRepository repository) =>
        repository.MemoryModules;
}

/// <summary>Diagnoses the PCIe devices.</summary>
public sealed class PCIeDiagnosticPass : ComponentPass
{
    public override string Category => "PCIe";

    protected override IEnumerable<IDiagnosable> Select(IComponentRepository repository) =>
        repository.PCIeDevices;
}
