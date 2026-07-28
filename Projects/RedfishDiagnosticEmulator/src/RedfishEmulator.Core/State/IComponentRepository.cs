using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.State;

/// <summary>
/// The single in-memory source of truth for the emulated hardware. Holds the
/// component resources as mutable state so diagnostics and fault injection can
/// change component health, and every reader (inventory, telemetry, diagnostics)
/// observes the same objects.
/// </summary>
public interface IComponentRepository
{
    SystemSeed System { get; }
    IReadOnlyList<Processor> Processors { get; }
    IReadOnlyList<Memory> MemoryModules { get; }
    IReadOnlyList<PCIeDevice> PCIeDevices { get; }
    IReadOnlyList<Chassis> Chassis { get; }

    /// <summary>All diagnosable components (processors, memory, PCIe devices).</summary>
    IEnumerable<IDiagnosable> DiagnosableComponents { get; }
}
