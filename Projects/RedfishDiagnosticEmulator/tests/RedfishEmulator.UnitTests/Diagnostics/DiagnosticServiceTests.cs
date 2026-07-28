using RedfishEmulator.Core.Diagnostics;
using RedfishEmulator.Core.Diagnostics.Passes;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;
using RedfishEmulator.Core.State;
using RedfishEmulator.UnitTests.Support;

namespace RedfishEmulator.UnitTests.Diagnostics;

/// <summary>Unit tests for the diagnostic service's task orchestration.</summary>
public sealed class DiagnosticServiceTests
{
    private static DiagnosticService ServiceOver(DiagTestSeed seed)
    {
        var repository = new InMemoryComponentRepository(seed);
        IDiagnosticPass[] passes =
        [
            new CpuDiagnosticPass(), new GpuDiagnosticPass(),
            new MemoryDiagnosticPass(), new PCIeDiagnosticPass(),
        ];
        var engine = new DiagnosticEngine(passes, repository, TimeProvider.System);
        var store = new InMemoryTaskStore(TimeProvider.System);
        return new DiagnosticService(engine, store, TimeProvider.System);
    }

    [Fact]
    public void StartDiagnostics_produces_a_completed_task_with_summary()
    {
        var task = ServiceOver(new DiagTestSeed()).StartDiagnostics(DiagTestSeed.SystemId);

        Assert.NotNull(task);
        Assert.Equal(TaskState.Completed, task!.TaskState);
        Assert.Equal(Health.OK, task.TaskStatus);
        Assert.Equal(100, task.PercentComplete);
        Assert.NotNull(task.EndTime);
        Assert.Contains(task.Messages, m => m.Text.Contains("7 passed"));
    }

    [Fact]
    public void StartDiagnostics_surfaces_a_failed_component_in_task_status_and_messages()
    {
        var seed = new DiagTestSeed();
        seed.Gpu1.Status.Health = Health.Critical;

        var task = ServiceOver(seed).StartDiagnostics(DiagTestSeed.SystemId);

        Assert.Equal(Health.Critical, task!.TaskStatus);
        Assert.Contains(task.Messages, m =>
            m.MessageId == "RedfishEmulator.1.0.ComponentFailed" && m.Text.Contains("GPU1"));
    }

    [Fact]
    public void StartDiagnostics_returns_null_for_unknown_system()
    {
        var task = ServiceOver(new DiagTestSeed()).StartDiagnostics("no-such-system");

        Assert.Null(task);
    }
}
