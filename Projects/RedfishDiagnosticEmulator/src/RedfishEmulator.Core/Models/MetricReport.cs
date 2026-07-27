using System.Text.Json.Serialization;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// A Redfish <c>MetricReport</c> — a timestamped snapshot of metric values (e.g.
/// GPU temperatures and utilization). Clients poll a report to observe the
/// platform's live telemetry.
/// </summary>
public sealed class MetricReport : ResourceBase
{
    /// <summary>When this report was generated.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The individual metric readings captured in this report.</summary>
    public IReadOnlyList<MetricReading> MetricValues { get; set; } = [];
}

/// <summary>
/// A single entry in a <see cref="MetricReport"/>. Redfish carries the reading as a
/// string in <c>MetricValue</c> and points back at its source via <c>MetricProperty</c>.
/// </summary>
public sealed class MetricReading
{
    public required string MetricId { get; set; }

    [JsonPropertyName("MetricValue")]
    public required string MetricValue { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>URI of the resource property this metric was sampled from.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MetricProperty { get; set; }
}
