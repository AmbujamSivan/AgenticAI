using RedfishEmulator.Core.State;

namespace RedfishEmulator.Core.Diagnostics;

/// <summary>
/// Default <see cref="IDiagnosticEngine"/>. Executes each registered pass over the
/// shared component state and folds the results into a single report. Because passes
/// read the live <see cref="IComponentRepository"/>, whatever state fault injection
/// has applied is reflected in the outcome.
/// </summary>
public sealed class DiagnosticEngine(
    IEnumerable<IDiagnosticPass> passes,
    IComponentRepository repository,
    TimeProvider time) : IDiagnosticEngine
{
    private readonly IReadOnlyList<IDiagnosticPass> _passes = passes.ToList();

    public DiagnosticReport? Run(string systemId)
    {
        if (systemId != repository.System.Id)
        {
            return null;
        }

        var startedAt = time.GetUtcNow();
        var results = _passes.SelectMany(pass => pass.Run(repository)).ToList();
        var completedAt = time.GetUtcNow();

        return new DiagnosticReport
        {
            SystemId = systemId,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Results = results,
        };
    }
}
