using System.Text.Json.Serialization;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The Redfish <c>ServiceRoot</c> — the single well-known entry point at
/// <c>/redfish/v1</c>. A client starts here and follows the navigation links to
/// discover every other resource the service exposes.
/// </summary>
public sealed class ServiceRoot : ResourceBase
{
    /// <summary>Version of the Redfish specification this service implements.</summary>
    public string RedfishVersion { get; set; } = "1.18.0";

    /// <summary>Stable unique identifier for this service instance.</summary>
    public string? UUID { get; set; }

    /// <summary>Product name advertised by the emulated BMC.</summary>
    public string Product { get; set; } = "Redfish Diagnostic & Telemetry Emulator";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NavigationLink? Systems { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NavigationLink? Chassis { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NavigationLink? Managers { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NavigationLink? TelemetryService { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NavigationLink? Tasks { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NavigationLink? SessionService { get; set; }
}
