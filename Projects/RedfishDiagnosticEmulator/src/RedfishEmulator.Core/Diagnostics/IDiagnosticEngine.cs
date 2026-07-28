namespace RedfishEmulator.Core.Diagnostics;

/// <summary>
/// Runs every registered <see cref="IDiagnosticPass"/> against a system and
/// aggregates the results into a <see cref="DiagnosticReport"/>. Returns
/// <c>null</c> if the target system does not exist.
/// </summary>
public interface IDiagnosticEngine
{
    DiagnosticReport? Run(string systemId);
}
