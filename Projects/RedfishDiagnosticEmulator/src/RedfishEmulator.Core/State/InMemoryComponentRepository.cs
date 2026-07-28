using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.State;

/// <summary>
/// Loads the seeded inventory once into mutable in-memory collections and holds it
/// for the process lifetime. The component instances are shared: mutating a
/// component's <see cref="Status"/> here is visible to every consumer of the repository.
/// </summary>
public sealed class InMemoryComponentRepository : IComponentRepository
{
    public InMemoryComponentRepository(IInventorySeedSource seed)
    {
        System = seed.Systems().Single();
        Processors = seed.Processors().ToList();
        MemoryModules = seed.MemoryModules().ToList();
        PCIeDevices = seed.PCIeDevices().ToList();
        Chassis = seed.Chassis().ToList();
    }

    public SystemSeed System { get; }
    public IReadOnlyList<Processor> Processors { get; }
    public IReadOnlyList<Memory> MemoryModules { get; }
    public IReadOnlyList<PCIeDevice> PCIeDevices { get; }
    public IReadOnlyList<Chassis> Chassis { get; }

    public IEnumerable<IDiagnosable> DiagnosableComponents =>
        Processors.Cast<IDiagnosable>()
            .Concat(MemoryModules)
            .Concat(PCIeDevices);
}
