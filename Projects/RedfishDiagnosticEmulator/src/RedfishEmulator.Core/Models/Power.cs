using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The Redfish <c>Power</c> sub-resource of a chassis: overall power draw,
/// voltage rails, and power supplies.
/// </summary>
public sealed class Power : ResourceBase
{
    public IReadOnlyList<PowerControl> PowerControl { get; set; } = [];
    public IReadOnlyList<Voltage> Voltages { get; set; } = [];
    public IReadOnlyList<PowerSupply> PowerSupplies { get; set; } = [];
}

/// <summary>Aggregate power draw and cap for a chassis (Redfish <c>PowerControl</c>).</summary>
public sealed class PowerControl
{
    public required string MemberId { get; set; }
    public required string Name { get; set; }
    public double PowerConsumedWatts { get; set; }
    public double? PowerCapacityWatts { get; set; }
    public Status Status { get; set; } = Status.Ok();
}

/// <summary>A voltage rail reading (Redfish <c>Voltage</c>).</summary>
public sealed class Voltage
{
    public required string MemberId { get; set; }
    public required string Name { get; set; }
    public double ReadingVolts { get; set; }
    public string? PhysicalContext { get; set; }
    public Status Status { get; set; } = Status.Ok();
}

/// <summary>A power supply unit (Redfish <c>PowerSupply</c>).</summary>
public sealed class PowerSupply
{
    public required string MemberId { get; set; }
    public required string Name { get; set; }
    public string PowerSupplyType { get; set; } = "AC";
    public double? LineInputVoltage { get; set; }
    public double? PowerCapacityWatts { get; set; }
    public Status Status { get; set; } = Status.Ok();
}
