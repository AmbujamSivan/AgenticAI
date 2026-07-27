using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The Redfish <c>Thermal</c> sub-resource of a chassis: temperature sensors and
/// cooling fans. (Classic Thermal model; newer Redfish also offers
/// ThermalSubsystem + Sensors — Thermal remains widely supported and clearer to demo.)
/// </summary>
public sealed class Thermal : ResourceBase
{
    public IReadOnlyList<Temperature> Temperatures { get; set; } = [];
    public IReadOnlyList<Fan> Fans { get; set; } = [];
}

/// <summary>A temperature sensor reading within a <see cref="Thermal"/> resource.</summary>
public sealed class Temperature
{
    public required string MemberId { get; set; }
    public required string Name { get; set; }
    public double ReadingCelsius { get; set; }
    public double? UpperThresholdCritical { get; set; }

    /// <summary>What the sensor is attached to, e.g. <c>CPU</c>, <c>GPU</c>, <c>Intake</c>.</summary>
    public string? PhysicalContext { get; set; }

    public Status Status { get; set; } = Status.Ok();
}

/// <summary>A cooling fan reading within a <see cref="Thermal"/> resource.</summary>
public sealed class Fan
{
    public required string MemberId { get; set; }
    public required string Name { get; set; }
    public double Reading { get; set; }
    public string ReadingUnits { get; set; } = "RPM";
    public Status Status { get; set; } = Status.Ok();
}
