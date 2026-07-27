using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Core.Telemetry;

/// <summary>
/// Produces the platform's live telemetry: chassis thermal/power sub-resources and
/// the metric reports exposed by the TelemetryService. Readings vary with time so
/// successive polls show movement. A <c>null</c> return means the requested chassis
/// or report does not exist (caller surfaces HTTP 404).
/// </summary>
public interface ITelemetryGenerator
{
    /// <summary>Ids of the metric reports this service publishes.</summary>
    IReadOnlyList<string> ReportIds { get; }

    Thermal? GetThermal(string chassisId);
    Power? GetPower(string chassisId);
    MetricReport? GetMetricReport(string reportId);
}
