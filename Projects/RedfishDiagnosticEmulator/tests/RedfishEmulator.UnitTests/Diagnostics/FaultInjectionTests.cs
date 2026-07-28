using RedfishEmulator.Core.Diagnostics.FaultInjection;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.State;
using RedfishEmulator.UnitTests.Support;

namespace RedfishEmulator.UnitTests.Diagnostics;

/// <summary>Unit tests for the fault profiles and the fault registry.</summary>
public sealed class FaultInjectionTests
{
    private static InMemoryComponentRepository Repo() => new(new DiagTestSeed());

    [Fact]
    public void GpuOffBus_takes_a_gpu_critical_and_offline_with_a_reason()
    {
        var repo = Repo();

        var affected = new GpuOffBusFault().Apply(repo);

        var gpu = repo.Processors.Single(p => p.Id == affected);
        Assert.Equal("GPU1", affected);
        Assert.Equal(Health.Critical, gpu.Status.Health);
        Assert.Equal(ResourceState.UnavailableOffline, gpu.Status.State);
        Assert.Contains("fallen off the PCIe bus", gpu.Status.Conditions!.Single().Message);
    }

    [Fact]
    public void MemoryEcc_degrades_a_dimm_to_warning_but_keeps_it_online()
    {
        var repo = Repo();

        var affected = new MemoryEccFault().Apply(repo);

        var dimm = repo.MemoryModules.Single(m => m.Id == affected);
        Assert.Equal(Health.Warning, dimm.Status.Health);
        Assert.Equal(ResourceState.Enabled, dimm.Status.State);
    }

    [Fact]
    public void ThermalTrip_takes_a_cpu_critical()
    {
        var repo = Repo();

        var affected = new ThermalTripFault().Apply(repo);

        var cpu = repo.Processors.Single(p => p.Id == affected);
        Assert.Equal(Health.Critical, cpu.Status.Health);
    }

    [Fact]
    public void Restore_returns_a_faulted_component_to_health()
    {
        var repo = Repo();
        var profile = new GpuOffBusFault();
        var affected = profile.Apply(repo)!;

        profile.Restore(repo, affected);

        var gpu = repo.Processors.Single(p => p.Id == affected);
        Assert.Equal(Health.OK, gpu.Status.Health);
        Assert.Equal(ResourceState.Enabled, gpu.Status.State);
        Assert.Null(gpu.Status.Conditions);
    }

    [Fact]
    public void Registry_activates_clears_and_reports_status()
    {
        var repo = Repo();
        var registry = new FaultRegistry([new GpuOffBusFault(), new MemoryEccFault()], repo);

        Assert.All(registry.Status(), s => Assert.False(s.Active));

        var activated = registry.Activate("GpuOffBus");
        Assert.True(activated!.Active);
        Assert.Equal("GPU1", activated.AffectedComponentId);
        Assert.Equal(Health.Critical, repo.Processors.Single(p => p.Id == "GPU1").Status.Health);

        var cleared = registry.Clear("GpuOffBus");
        Assert.False(cleared!.Active);
        Assert.Equal(Health.OK, repo.Processors.Single(p => p.Id == "GPU1").Status.Health);
    }

    [Fact]
    public void Registry_activation_is_idempotent()
    {
        var repo = Repo();
        var registry = new FaultRegistry([new GpuOffBusFault()], repo);

        registry.Activate("GpuOffBus");
        registry.Activate("GpuOffBus");   // second activation must not fault a second GPU

        var critical = repo.Processors.Count(p => p.Status.Health == Health.Critical);
        Assert.Equal(1, critical);
    }

    [Fact]
    public void Registry_clearAll_restores_every_fault()
    {
        var repo = Repo();
        var registry = new FaultRegistry(
            [new GpuOffBusFault(), new MemoryEccFault(), new ThermalTripFault()], repo);
        registry.Activate("GpuOffBus");
        registry.Activate("MemoryEcc");
        registry.Activate("ThermalTrip");

        registry.ClearAll();

        Assert.All(repo.DiagnosableComponents, c => Assert.Equal(Health.OK, c.Status.Health));
        Assert.All(registry.Status(), s => Assert.False(s.Active));
    }

    [Fact]
    public void Registry_returns_null_for_unknown_profile()
    {
        var registry = new FaultRegistry([new GpuOffBusFault()], Repo());

        Assert.Null(registry.Activate("NoSuchProfile"));
        Assert.Null(registry.Clear("NoSuchProfile"));
    }
}
