using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>Serves the Redfish Chassis collection and individual Chassis resources.</summary>
[ApiController]
[Produces("application/json")]
public sealed class ChassisController(IInventoryService inventory) : ControllerBase
{
    [HttpGet("/redfish/v1/Chassis")]
    public ResourceCollection GetChassis() => inventory.GetChassisCollection();

    [HttpGet("/redfish/v1/Chassis/{chassisId}")]
    public ActionResult<Chassis> GetChassisItem(string chassisId) =>
        inventory.GetChassis(chassisId) is { } chassis ? chassis : NotFound();
}
