using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Telemetry;

namespace RedfishEmulator.Api.Controllers;

/// <summary>
/// Serves the Redfish TelemetryService and its MetricReports — the live platform
/// telemetry (CPU/GPU temperatures and utilization, platform power draw).
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class TelemetryServiceController(ITelemetryGenerator telemetry) : ControllerBase
{
    private const string Base = "/redfish/v1/TelemetryService";

    [HttpGet(Base)]
    public TelemetryService GetTelemetryService() => new()
    {
        ODataId = Base,
        ODataType = "#TelemetryService.v1_3_1.TelemetryService",
        ODataContext = "/redfish/v1/$metadata#TelemetryService.TelemetryService",
        Id = "TelemetryService",
        Name = "Telemetry Service",
        MetricReports = new NavigationLink($"{Base}/MetricReports"),
    };

    [HttpGet(Base + "/MetricReports")]
    public ResourceCollection GetMetricReports() => ResourceCollection.Of(
        $"{Base}/MetricReports", "MetricReportCollection", "Metric Report Collection",
        telemetry.ReportIds.Select(id => $"{Base}/MetricReports/{id}"));

    [HttpGet(Base + "/MetricReports/{reportId}")]
    public ActionResult<MetricReport> GetMetricReport(string reportId) =>
        telemetry.GetMetricReport(reportId) is { } report ? report : NotFound();
}
