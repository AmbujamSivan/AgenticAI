using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Core.Services;

/// <summary>
/// Orchestrates a diagnostic run behind the Redfish async-task model: triggers the
/// engine and records the outcome as a <see cref="RedfishTask"/> the client can poll.
/// Returns <c>null</c> if the target system does not exist.
/// </summary>
public interface IDiagnosticService
{
    RedfishTask? StartDiagnostics(string systemId);
}
