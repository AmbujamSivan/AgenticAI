namespace RedfishEmulator.Core.Models.Common;

/// <summary>
/// A hardware component the diagnostic engine can evaluate: it has an identity and
/// a mutable <see cref="Status"/>. Implemented by the leaf inventory resources
/// (Processor, Memory, PCIeDevice) so a diagnostic pass can treat them uniformly.
/// </summary>
public interface IDiagnosable
{
    string? Id { get; }
    string? Name { get; }
    Status Status { get; }
}
