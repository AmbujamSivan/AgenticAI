using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.UnitTests.Support;

/// <summary>
/// A small, healthy-by-default inventory seed for diagnostics tests: 2 CPUs, 2 GPUs,
/// 2 DIMMs, 1 PCIe device (7 diagnosable components). Component instances are exposed
/// so a test can degrade one before running diagnostics.
/// </summary>
public sealed class DiagTestSeed : IInventorySeedSource
{
    public const string SystemId = "1";

    public Processor Cpu1 { get; } = Proc("CPU1", ProcessorType.CPU);
    public Processor Gpu1 { get; } = Proc("GPU1", ProcessorType.GPU);
    public Memory Dimm1 { get; } = Dimm("DIMM1");
    public PCIeDevice Nic1 { get; } = Pcie("NIC1");

    public IReadOnlyList<SystemSeed> Systems() => [new SystemSeed { Id = SystemId, Name = "Test System" }];

    public IReadOnlyList<Processor> Processors() =>
        [Cpu1, Proc("CPU2", ProcessorType.CPU), Gpu1, Proc("GPU2", ProcessorType.GPU)];

    public IReadOnlyList<Memory> MemoryModules() => [Dimm1, Dimm("DIMM2")];
    public IReadOnlyList<PCIeDevice> PCIeDevices() => [Nic1];

    public IReadOnlyList<Chassis> Chassis() =>
        [new Chassis { ODataId = "/redfish/v1/Chassis/1", ODataType = "#Chassis.v1.Chassis", Id = "1" }];

    private static Processor Proc(string id, ProcessorType type) => new()
    {
        ODataId = $"/redfish/v1/Systems/1/Processors/{id}",
        ODataType = "#Processor.v1.Processor",
        Id = id,
        Name = id,
        ProcessorType = type,
    };

    private static Memory Dimm(string id) => new()
    {
        ODataId = $"/redfish/v1/Systems/1/Memory/{id}",
        ODataType = "#Memory.v1.Memory",
        Id = id,
        Name = id,
    };

    private static PCIeDevice Pcie(string id) => new()
    {
        ODataId = $"/redfish/v1/Systems/1/PCIeDevices/{id}",
        ODataType = "#PCIeDevice.v1.PCIeDevice",
        Id = id,
        Name = id,
    };
}
