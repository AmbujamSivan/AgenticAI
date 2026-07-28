using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Api.Controllers;

/// <summary>
/// Serves the Redfish service entry points:
/// <c>GET /redfish</c> (protocol version map) and
/// <c>GET /redfish/v1</c> (the ServiceRoot).
/// </summary>
[ApiController]
[AllowAnonymous]   // ServiceRoot and the version map are unauthenticated per the Redfish spec.
public sealed class ServiceRootController : ControllerBase
{
    /// <summary>
    /// The Redfish protocol version map. A client hits this first to discover
    /// which major versions are available.
    /// </summary>
    [HttpGet("/redfish")]
    [Produces("application/json")]
    public IDictionary<string, string> GetVersions() =>
        new Dictionary<string, string> { ["v1"] = "/redfish/v1/" };

    /// <summary>The ServiceRoot: the top of the resource tree at <c>/redfish/v1</c>.</summary>
    [HttpGet("/redfish/v1")]
    [Produces("application/json")]
    public ServiceRoot GetServiceRoot() => new()
    {
        ODataId = "/redfish/v1",
        ODataType = "#ServiceRoot.v1_16_0.ServiceRoot",
        ODataContext = "/redfish/v1/$metadata#ServiceRoot.ServiceRoot",
        Id = "RootService",
        Name = "Root Service",
        RedfishVersion = "1.18.0",
        UUID = "92384634-2938-2342-8820-489239905423",
        Systems = new NavigationLink("/redfish/v1/Systems"),
        Chassis = new NavigationLink("/redfish/v1/Chassis"),
        Managers = new NavigationLink("/redfish/v1/Managers"),
        TelemetryService = new NavigationLink("/redfish/v1/TelemetryService"),
        Tasks = new NavigationLink("/redfish/v1/TaskService"),
        SessionService = new NavigationLink("/redfish/v1/SessionService"),
        Oem = new Dictionary<string, object>
        {
            ["RedfishEmulator"] = new
            {
                FaultInjection = new NavigationLink("/redfish/v1/Oem/RedfishEmulator/FaultInjection"),
            },
        },
    };
}
