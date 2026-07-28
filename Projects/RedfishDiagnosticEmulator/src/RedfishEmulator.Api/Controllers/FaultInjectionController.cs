using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Diagnostics.FaultInjection;

namespace RedfishEmulator.Api.Controllers;

/// <summary>
/// OEM endpoint for injecting edge-case hardware failure modes into the emulator.
/// Not part of the Redfish standard — hosted under the <c>Oem</c> path — it lets a
/// client (or the stress-test suite) drive components into fault states and observe
/// how inventory, telemetry, and diagnostics respond.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class FaultInjectionController(IFaultRegistry faults) : ControllerBase
{
    private const string Base = "/redfish/v1/Oem/RedfishEmulator/FaultInjection";

    /// <summary>Lists the available fault profiles and their current active state.</summary>
    [HttpGet(Base)]
    public FaultInjectionResource Get() => new()
    {
        ODataId = Base,
        Name = "Fault Injection",
        Profiles = faults.Status(),
    };

    /// <summary>Activates a fault profile, applying it to an eligible component.</summary>
    [HttpPost(Base + "/{profileId}/Activate")]
    public ActionResult<FaultProfileStatus> Activate(string profileId) =>
        faults.Activate(profileId) is { } status ? status : NotFound();

    /// <summary>Clears a fault profile, restoring its component to health.</summary>
    [HttpPost(Base + "/{profileId}/Clear")]
    public ActionResult<FaultProfileStatus> Clear(string profileId) =>
        faults.Clear(profileId) is { } status ? status : NotFound();

    /// <summary>Clears every active fault, returning the whole platform to health.</summary>
    [HttpPost(Base + "/Reset")]
    public IActionResult Reset()
    {
        faults.ClearAll();
        return NoContent();
    }
}

/// <summary>Response body for the fault-injection listing.</summary>
public sealed class FaultInjectionResource
{
    [System.Text.Json.Serialization.JsonPropertyName("@odata.id")]
    public required string ODataId { get; set; }

    public required string Name { get; set; }

    public required IReadOnlyList<FaultProfileStatus> Profiles { get; set; }
}
