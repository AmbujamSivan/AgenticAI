using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>Serves a system's Memory collection and individual DIMM resources.</summary>
[ApiController]
[Produces("application/json")]
public sealed class MemoryController(IInventoryService inventory) : ControllerBase
{
    [HttpGet("/redfish/v1/Systems/{systemId}/Memory")]
    public ActionResult<ResourceCollection> GetMemory(string systemId) =>
        inventory.GetMemory(systemId) is { } collection ? collection : NotFound();

    [HttpGet("/redfish/v1/Systems/{systemId}/Memory/{memoryId}")]
    public ActionResult<Memory> GetMemoryModule(string systemId, string memoryId) =>
        inventory.GetMemoryModule(systemId, memoryId) is { } memory ? memory : NotFound();
}
