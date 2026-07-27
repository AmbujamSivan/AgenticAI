using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>Serves the Redfish Systems collection and individual ComputerSystem resources.</summary>
[ApiController]
[Produces("application/json")]
public sealed class SystemsController(IInventoryService inventory) : ControllerBase
{
    /// <summary>The collection of computer systems managed by this BMC.</summary>
    [HttpGet("/redfish/v1/Systems")]
    public ResourceCollection GetSystems() => inventory.GetSystems();

    /// <summary>A single computer system, including live processor/memory summaries and health.</summary>
    [HttpGet("/redfish/v1/Systems/{systemId}")]
    public ActionResult<ComputerSystem> GetSystem(string systemId) =>
        inventory.GetSystem(systemId) is { } system ? system : NotFound();
}
