using RedfishEmulator.Core.Diagnostics;
using RedfishEmulator.Core.Diagnostics.Passes;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.State;
using RedfishEmulator.UnitTests.Support;

namespace RedfishEmulator.UnitTests.Diagnostics;

/// <summary>Unit tests for the diagnostic engine and its pluggable passes.</summary>
public sealed class DiagnosticEngineTests
{
    private static DiagnosticEngine EngineOver(DiagTestSeed seed)
    {
        var repository = new InMemoryComponentRepository(seed);
        IDiagnosticPass[] passes =
        [
            new CpuDiagnosticPass(), new GpuDiagnosticPass(),
            new MemoryDiagnosticPass(), new PCIeDiagnosticPass(),
        ];
        return new DiagnosticEngine(passes, repository, TimeProvider.System);
    }

    [Fact]
    public void Healthy_system_passes_every_component()
    {
        var report = EngineOver(new DiagTestSeed()).Run(DiagTestSeed.SystemId);

        Assert.NotNull(report);
        Assert.Equal(7, report!.Results.Count);          // 2 CPU + 2 GPU + 2 DIMM + 1 PCIe
        Assert.Equal(DiagnosticOutcome.Pass, report.Overall);
        Assert.Equal(7, report.PassedCount);
        Assert.Equal(Health.OK, report.OverallHealth);
    }

    [Fact]
    public void Critical_component_fails_the_run_and_is_reported()
    {
        var seed = new DiagTestSeed();
        seed.Gpu1.Status.Health = Health.Critical;   // "GPU fell off the bus"

        var report = EngineOver(seed).Run(DiagTestSeed.SystemId);

        Assert.Equal(DiagnosticOutcome.Fail, report!.Overall);
        Assert.Equal(Health.Critical, report.OverallHealth);
        Assert.Equal(1, report.FailedCount);

        var gpuResult = Assert.Single(report.Results, r => r.ComponentId == "GPU1");
        Assert.Equal(DiagnosticOutcome.Fail, gpuResult.Outcome);
        Assert.Equal("GPU Accelerator", gpuResult.Category);
    }

    [Fact]
    public void Warning_component_warns_without_failing()
    {
        var seed = new DiagTestSeed();
        seed.Dimm1.Status.Health = Health.Warning;

        var report = EngineOver(seed).Run(DiagTestSeed.SystemId);

        Assert.Equal(DiagnosticOutcome.Warning, report!.Overall);
        Assert.Equal(1, report.WarningCount);
        Assert.Equal(0, report.FailedCount);
    }

    [Fact]
    public void Unknown_system_returns_null()
    {
        var report = EngineOver(new DiagTestSeed()).Run("no-such-system");

        Assert.Null(report);
    }
}
