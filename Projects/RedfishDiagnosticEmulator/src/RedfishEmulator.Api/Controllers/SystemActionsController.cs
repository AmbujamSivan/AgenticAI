using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>Handles OEM actions invoked on a ComputerSystem.</summary>
[ApiController]
[Produces("application/json")]
public sealed class SystemActionsController(IDiagnosticService diagnostics) : ControllerBase
{
    /// <summary>
    /// Kicks off an automated diagnostic pass over the system's components. Follows the
    /// Redfish async pattern: replies <c>202 Accepted</c> with a <c>Location</c> header
    /// pointing at the task the client polls for completion and results.
    /// </summary>
    [HttpPost("/redfish/v1/Systems/{systemId}/Actions/Oem/RedfishEmulator.RunDiagnostics")]
    public ActionResult<RedfishTask> RunDiagnostics(string systemId)
    {
        if (diagnostics.StartDiagnostics(systemId) is not { } task)
        {
            return NotFound();
        }

        Response.Headers.Location = task.ODataId;
        return Accepted(task.ODataId, task);
    }
}
