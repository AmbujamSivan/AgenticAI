using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Services;

/// <summary>
/// Default <see cref="IInventoryService"/>. Loads and caches the seeded inventory
/// once, serves leaf resources directly, and composes the <see cref="ComputerSystem"/>
/// on read so its processor/memory summaries and health rollup always reflect the
/// current component state (which diagnostics and fault injection mutate in later phases).
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private const string SystemsBase = "/redfish/v1/Systems";
    private const string ChassisBase = "/redfish/v1/Chassis";

    private readonly SystemSeed _system;
    private readonly IReadOnlyList<Processor> _processors;
    private readonly IReadOnlyList<Memory> _memory;
    private readonly IReadOnlyList<PCIeDevice> _pcieDevices;
    private readonly IReadOnlyDictionary<string, Chassis> _chassis;

    public InventoryService(IInventorySeedSource seed)
    {
        // Phase 2 models a single system; take the first seeded system as "the box".
        _system = seed.Systems().Single();
        _processors = seed.Processors();
        _memory = seed.MemoryModules();
        _pcieDevices = seed.PCIeDevices();
        _chassis = seed.Chassis().ToDictionary(c => c.Id!);
    }

    public ResourceCollection GetSystems() => ResourceCollection.Of(
        SystemsBase, "ComputerSystemCollection", "Computer System Collection",
        [$"{SystemsBase}/{_system.Id}"]);

    public ComputerSystem? GetSystem(string systemId)
    {
        if (systemId != _system.Id)
        {
            return null;
        }

        var componentStatuses = _processors.Select(p => p.Status)
            .Concat(_memory.Select(m => m.Status))
            .Concat(_pcieDevices.Select(d => d.Status));

        var cpus = _processors.Where(p => p.ProcessorType == ProcessorType.CPU).ToList();
        var totalMemoryGiB = _memory.Sum(m => m.CapacityMiB ?? 0) / 1024.0;

        return new ComputerSystem
        {
            ODataId = $"{SystemsBase}/{_system.Id}",
            ODataType = "#ComputerSystem.v1_22_0.ComputerSystem",
            ODataContext = "/redfish/v1/$metadata#ComputerSystem.ComputerSystem",
            Id = _system.Id,
            Name = _system.Name,
            SystemType = _system.SystemType,
            Manufacturer = _system.Manufacturer,
            Model = _system.Model,
            SerialNumber = _system.SerialNumber,
            SKU = _system.SKU,
            UUID = _system.UUID,
            BiosVersion = _system.BiosVersion,
            PowerState = _system.PowerState,
            Status = new Status
            {
                State = ResourceState.Enabled,
                Health = HealthRollup.Of(componentStatuses),
                HealthRollup = HealthRollup.Of(componentStatuses),
            },
            ProcessorSummary = new ProcessorSummary
            {
                Count = cpus.Count,
                Model = cpus.FirstOrDefault()?.Model,
                Status = new Status
                {
                    State = ResourceState.Enabled,
                    Health = HealthRollup.Of(cpus.Select(c => c.Status)),
                },
            },
            MemorySummary = new MemorySummary
            {
                TotalSystemMemoryGiB = totalMemoryGiB,
                Status = new Status
                {
                    State = ResourceState.Enabled,
                    Health = HealthRollup.Of(_memory.Select(m => m.Status)),
                },
            },
            Processors = new NavigationLink($"{SystemsBase}/{_system.Id}/Processors"),
            Memory = new NavigationLink($"{SystemsBase}/{_system.Id}/Memory"),
            PCIeDevices = new NavigationLink($"{SystemsBase}/{_system.Id}/PCIeDevices"),
        };
    }

    public ResourceCollection? GetProcessors(string systemId) =>
        RequireSystem(systemId) is null
            ? null
            : ResourceCollection.Of(
                $"{SystemsBase}/{systemId}/Processors", "ProcessorCollection",
                "Processor Collection", _processors.Select(p => p.ODataId));

    public Processor? GetProcessor(string systemId, string processorId) =>
        RequireSystem(systemId) is null
            ? null
            : _processors.FirstOrDefault(p => p.Id == processorId);

    public ResourceCollection? GetMemory(string systemId) =>
        RequireSystem(systemId) is null
            ? null
            : ResourceCollection.Of(
                $"{SystemsBase}/{systemId}/Memory", "MemoryCollection",
                "Memory Collection", _memory.Select(m => m.ODataId));

    public Memory? GetMemoryModule(string systemId, string memoryId) =>
        RequireSystem(systemId) is null
            ? null
            : _memory.FirstOrDefault(m => m.Id == memoryId);

    public ResourceCollection? GetPCIeDevices(string systemId) =>
        RequireSystem(systemId) is null
            ? null
            : ResourceCollection.Of(
                $"{SystemsBase}/{systemId}/PCIeDevices", "PCIeDeviceCollection",
                "PCIe Device Collection", _pcieDevices.Select(d => d.ODataId));

    public PCIeDevice? GetPCIeDevice(string systemId, string deviceId) =>
        RequireSystem(systemId) is null
            ? null
            : _pcieDevices.FirstOrDefault(d => d.Id == deviceId);

    public ResourceCollection GetChassisCollection() => ResourceCollection.Of(
        ChassisBase, "ChassisCollection", "Chassis Collection",
        _chassis.Values.Select(c => c.ODataId));

    public Chassis? GetChassis(string chassisId)
    {
        if (_chassis.GetValueOrDefault(chassisId) is not { } chassis)
        {
            return null;
        }

        // Telemetry sub-resources are derived from the chassis URI (served in Phase 3).
        chassis.Thermal = new NavigationLink($"{chassis.ODataId}/Thermal");
        chassis.Power = new NavigationLink($"{chassis.ODataId}/Power");
        return chassis;
    }

    /// <summary>Returns the seeded system id when it matches, else null (unknown system → 404).</summary>
    private string? RequireSystem(string systemId) =>
        systemId == _system.Id ? _system.Id : null;
}
