using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Core.Inventory;

/// <summary>
/// Supplies the emulator's initial hardware inventory. Implemented outside Core
/// (e.g. by <c>RedfishEmulator.Data</c> loading JSON), so the domain services
/// depend only on this abstraction — not on where the seed data comes from.
/// </summary>
/// <remarks>
/// Phase 2 models a single system; all processors, memory modules and PCIe
/// devices returned here belong to that system.
/// </remarks>
public interface IInventorySeedSource
{
    IReadOnlyList<SystemSeed> Systems();
    IReadOnlyList<Processor> Processors();
    IReadOnlyList<Memory> MemoryModules();
    IReadOnlyList<PCIeDevice> PCIeDevices();
    IReadOnlyList<Chassis> Chassis();
}
