using RedfishEmulator.Core.Inventory;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="InventoryService"/> — the composition logic that
/// builds a ComputerSystem's summaries and health rollup from its components.
/// A hand-built seed source keeps these tests independent of the JSON data.
/// </summary>
public sealed class InventoryServiceTests
{
    private const string SysId = "1";

    [Fact]
    public void GetSystem_counts_only_cpus_in_processor_summary()
    {
        var service = new InventoryService(new FakeSeed());

        var system = service.GetSystem(SysId);

        Assert.NotNull(system);
        Assert.Equal(2, system!.ProcessorSummary!.Count);          // 2 CPUs, GPU excluded
        Assert.Equal("Xeon", system.ProcessorSummary.Model);
    }

    [Fact]
    public void GetSystem_totals_memory_in_gibibytes()
    {
        var service = new InventoryService(new FakeSeed());

        var system = service.GetSystem(SysId);

        // 2 x 65536 MiB = 131072 MiB = 128 GiB.
        Assert.Equal(128.0, system!.MemorySummary!.TotalSystemMemoryGiB);
    }

    [Fact]
    public void GetSystem_rolls_a_failed_gpu_up_into_system_health()
    {
        var seed = new FakeSeed();
        seed.Processors().Single(p => p.Id == "GPU1").Status.Health = Health.Critical;

        var system = new InventoryService(seed).GetSystem(SysId);

        // The whole-system rollup reflects the worst component...
        Assert.Equal(Health.Critical, system!.Status.Health);
        Assert.Equal(Health.Critical, system.Status.HealthRollup);
        // ...but the CPU-only ProcessorSummary stays healthy.
        Assert.Equal(Health.OK, system.ProcessorSummary!.Status.Health);
    }

    [Fact]
    public void Unknown_system_returns_null_for_all_lookups()
    {
        var service = new InventoryService(new FakeSeed());

        Assert.Null(service.GetSystem("99"));
        Assert.Null(service.GetProcessors("99"));
        Assert.Null(service.GetProcessor("99", "CPU1"));
        Assert.Null(service.GetMemory("99"));
    }

    [Fact]
    public void Processor_collection_lists_every_processor()
    {
        var service = new InventoryService(new FakeSeed());

        var collection = service.GetProcessors(SysId);

        Assert.NotNull(collection);
        Assert.Equal(3, collection!.MembersODataCount);   // 2 CPU + 1 GPU
    }

    /// <summary>Minimal in-memory seed: two CPUs, one GPU, two DIMMs, one chassis.</summary>
    private sealed class FakeSeed : IInventorySeedSource
    {
        private readonly List<Processor> _processors =
        [
            Cpu("CPU1"), Cpu("CPU2"), Gpu("GPU1"),
        ];

        private readonly List<Memory> _memory = [Dimm("DIMM1"), Dimm("DIMM2")];

        public IReadOnlyList<SystemSeed> Systems() =>
            [new SystemSeed { Id = SysId, Name = "Test System" }];

        public IReadOnlyList<Processor> Processors() => _processors;
        public IReadOnlyList<Memory> MemoryModules() => _memory;
        public IReadOnlyList<PCIeDevice> PCIeDevices() => [];

        public IReadOnlyList<Chassis> Chassis() =>
        [
            new Chassis { ODataId = "/redfish/v1/Chassis/1", ODataType = "#Chassis.v1.Chassis", Id = "1" },
        ];

        private static Processor Cpu(string id) => new()
        {
            ODataId = $"/redfish/v1/Systems/1/Processors/{id}",
            ODataType = "#Processor.v1.Processor",
            Id = id,
            ProcessorType = ProcessorType.CPU,
            Model = "Xeon",
        };

        private static Processor Gpu(string id) => new()
        {
            ODataId = $"/redfish/v1/Systems/1/Processors/{id}",
            ODataType = "#Processor.v1.Processor",
            Id = id,
            ProcessorType = ProcessorType.GPU,
            Model = "H100",
        };

        private static Memory Dimm(string id) => new()
        {
            ODataId = $"/redfish/v1/Systems/1/Memory/{id}",
            ODataType = "#Memory.v1.Memory",
            Id = id,
            CapacityMiB = 65536,
        };
    }
}
