using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Telemetry;

namespace RedfishEmulator.Api.Controllers;

/// <summary>Serves a chassis's Thermal and Power telemetry sub-resources.</summary>
[ApiController]
[Produces("application/json")]
public sealed class ChassisTelemetryController(ITelemetryGenerator telemetry) : ControllerBase
{
    [HttpGet("/redfish/v1/Chassis/{chassisId}/Thermal")]
    public ActionResult<Thermal> GetThermal(string chassisId) =>
        telemetry.GetThermal(chassisId) is { } thermal ? thermal : NotFound();

    [HttpGet("/redfish/v1/Chassis/{chassisId}/Power")]
    public ActionResult<Power> GetPower(string chassisId) =>
        telemetry.GetPower(chassisId) is { } power ? power : NotFound();
}
