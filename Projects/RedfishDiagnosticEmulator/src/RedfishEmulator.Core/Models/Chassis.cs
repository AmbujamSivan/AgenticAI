using System.Text.Json.Serialization;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// A Redfish <c>Chassis</c> — the physical enclosure. Carries identity and power
/// state now; Thermal and Power sensor sub-resources are added in Phase 3.
/// </summary>
public sealed class Chassis : ResourceBase
{
    /// <summary>Physical form factor, e.g. <c>RackMount</c>.</summary>
    public string ChassisType { get; set; } = "RackMount";

    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SKU { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PowerState PowerState { get; set; } = PowerState.On;

    public Status Status { get; set; } = Status.Ok();

    /// <summary>Link to the chassis thermal sub-resource (temperatures + fans).</summary>
    public NavigationLink? Thermal { get; set; }

    /// <summary>Link to the chassis power sub-resource (draw, voltages, PSUs).</summary>
    public NavigationLink? Power { get; set; }
}
