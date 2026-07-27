using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>
/// Serves a system's Processors — both host CPUs and GPU accelerators, which
/// Redfish inventories as the same resource type distinguished by ProcessorType.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class ProcessorsController(IInventoryService inventory) : ControllerBase
{
    [HttpGet("/redfish/v1/Systems/{systemId}/Processors")]
    public ActionResult<ResourceCollection> GetProcessors(string systemId) =>
        inventory.GetProcessors(systemId) is { } collection ? collection : NotFound();

    [HttpGet("/redfish/v1/Systems/{systemId}/Processors/{processorId}")]
    public ActionResult<Processor> GetProcessor(string systemId, string processorId) =>
        inventory.GetProcessor(systemId, processorId) is { } processor ? processor : NotFound();
}
