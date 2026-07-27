using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Inventory;

/// <summary>
/// The seeded "facts" of a system — its physical identity, independent of the
/// Redfish envelope. The <see cref="Services.IInventoryService"/> composes these
/// facts with live processor/memory summaries and OData identity to produce the
/// full <see cref="Models.ComputerSystem"/> resource on read.
/// </summary>
public sealed record SystemSeed
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string SystemType { get; init; } = "Physical";
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public string? SKU { get; init; }
    public string? UUID { get; init; }
    public string? BiosVersion { get; init; }
    public PowerState PowerState { get; init; } = PowerState.On;
}
