using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.State;

namespace RedfishEmulator.Core.Diagnostics;

/// <summary>
/// One category of diagnostic checks (CPUs, GPUs, memory, PCIe…). Passes are
/// registered and composed by the <see cref="IDiagnosticEngine"/>, so a new category
/// of checks is added by implementing this interface — no change to the engine.
/// </summary>
public interface IDiagnosticPass
{
    /// <summary>Human-readable category name, e.g. <c>GPU Accelerator</c>.</summary>
    string Category { get; }

    /// <summary>Runs the checks for this category against the current component state.</summary>
    IEnumerable<DiagnosticResult> Run(IComponentRepository repository);
}

/// <summary>
/// Base for passes that evaluate a set of <see cref="IDiagnosable"/> components by
/// their reported health. A component that is Critical fails, Warning warns, and
/// anything else passes — so a fault injected into a component (Phase 5) is detected
/// here without changing the pass.
/// </summary>
public abstract class ComponentPass : IDiagnosticPass
{
    public abstract string Category { get; }

    /// <summary>Selects the components this pass is responsible for.</summary>
    protected abstract IEnumerable<IDiagnosable> Select(IComponentRepository repository);

    public IEnumerable<DiagnosticResult> Run(IComponentRepository repository) =>
        Select(repository).Select(Evaluate).ToList();

    private DiagnosticResult Evaluate(IDiagnosable component)
    {
        var id = component.Id ?? "unknown";
        var label = $"{component.Name ?? id} ({id})";

        var (outcome, message) = component.Status.Health switch
        {
            Health.Critical => (DiagnosticOutcome.Fail, $"{label} reported Critical health and failed diagnostics."),
            Health.Warning => (DiagnosticOutcome.Warning, $"{label} reported a degraded (Warning) condition."),
            _ => (DiagnosticOutcome.Pass, $"{label} passed all diagnostic checks."),
        };

        return new DiagnosticResult(Category, id, component.Name, outcome, message);
    }
}
