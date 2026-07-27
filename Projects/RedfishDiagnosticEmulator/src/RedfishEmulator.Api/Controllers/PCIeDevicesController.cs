using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>Serves a system's PCIe device collection and individual PCIeDevice resources.</summary>
[ApiController]
[Produces("application/json")]
public sealed class PCIeDevicesController(IInventoryService inventory) : ControllerBase
{
    [HttpGet("/redfish/v1/Systems/{systemId}/PCIeDevices")]
    public ActionResult<ResourceCollection> GetPCIeDevices(string systemId) =>
        inventory.GetPCIeDevices(systemId) is { } collection ? collection : NotFound();

    [HttpGet("/redfish/v1/Systems/{systemId}/PCIeDevices/{deviceId}")]
    public ActionResult<PCIeDevice> GetPCIeDevice(string systemId, string deviceId) =>
        inventory.GetPCIeDevice(systemId, deviceId) is { } device ? device : NotFound();
}
