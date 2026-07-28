using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Core.Metadata;
using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Api.Controllers;

/// <summary>
/// Serves the two OData discovery documents the Redfish protocol requires
/// alongside the ServiceRoot: the CSDL metadata document (<c>$metadata</c>) and
/// the OData service document (<c>odata</c>).
/// </summary>
[ApiController]
[AllowAnonymous]   // The OData metadata and service documents are unauthenticated per the Redfish spec.
public sealed class MetadataController : ControllerBase
{
    /// <summary>The OData CSDL/EDMX metadata document describing the service schema.</summary>
    [HttpGet("/redfish/v1/$metadata")]
    [Produces("application/xml")]
    public ContentResult GetMetadata() =>
        Content(RedfishMetadataDocument.Build(), "application/xml");

    /// <summary>The OData service document listing top-level resources.</summary>
    [HttpGet("/redfish/v1/odata")]
    [Produces("application/json")]
    public OdataServiceDocument GetServiceDocument() => new()
    {
        Value =
        [
            new OdataServiceEntry { Name = "Service", Url = "/redfish/v1" },
            new OdataServiceEntry { Name = "Systems", Url = "/redfish/v1/Systems" },
            new OdataServiceEntry { Name = "Chassis", Url = "/redfish/v1/Chassis" },
            new OdataServiceEntry { Name = "Managers", Url = "/redfish/v1/Managers" },
            new OdataServiceEntry { Name = "TelemetryService", Url = "/redfish/v1/TelemetryService" },
            new OdataServiceEntry { Name = "TaskService", Url = "/redfish/v1/TaskService" },
            new OdataServiceEntry { Name = "SessionService", Url = "/redfish/v1/SessionService" },
        ],
    };
}
