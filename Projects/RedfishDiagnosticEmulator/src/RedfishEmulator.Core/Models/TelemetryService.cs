using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The Redfish <c>TelemetryService</c> at <c>/redfish/v1/TelemetryService</c> —
/// the entry point to platform metrics, linking to the collection of metric reports.
/// </summary>
public sealed class TelemetryService : ResourceBase
{
    public Status Status { get; set; } = Status.Ok();

    public NavigationLink? MetricReports { get; set; }
}
