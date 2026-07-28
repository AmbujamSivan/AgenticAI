using System.Text.Json.Serialization;

namespace RedfishEmulator.Core.Models.Common;

/// <summary>Operational state of a resource (subset of the Redfish <c>State</c> enum).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResourceState
{
    Enabled,
    Disabled,
    StandbyOffline,
    InTest,
    Starting,
    Absent,
    UnavailableOffline,
    Updating,
}

/// <summary>Health of a resource per the Redfish <c>Health</c> enum.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Health
{
    OK,
    Warning,
    Critical,
}

/// <summary>
/// The standard Redfish <c>Status</c> object. Reported on every hardware resource
/// so a client can assess condition at a glance. <see cref="HealthRollup"/>
/// aggregates the health of subordinate resources (e.g. a system rolls up the
/// worst health of its processors, memory and PCIe devices).
/// </summary>
public sealed class Status
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceState? State { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Health? Health { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Health? HealthRollup { get; set; }

    /// <summary>
    /// Active conditions explaining a non-OK status (Redfish <c>Status.Conditions</c>).
    /// Fault injection populates this so a component reports *why* it is unhealthy.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Condition>? Conditions { get; set; }

    /// <summary>A healthy, enabled status with no subordinate rollup set.</summary>
    public static Status Ok() => new() { State = ResourceState.Enabled, Health = Common.Health.OK };
}

/// <summary>
/// A Redfish <c>Condition</c> — a message describing why a resource is in its
/// current (usually degraded) state.
/// </summary>
public sealed class Condition
{
    public required string MessageId { get; set; }

    public required string Message { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Health? Severity { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Timestamp { get; set; }
}
