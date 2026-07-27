using System.Text.Json.Serialization;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The OData service document served at <c>/redfish/v1/odata</c>. It lists the
/// top-level resources reachable from the service so generic OData tooling can
/// enumerate them without Redfish-specific knowledge.
/// </summary>
public sealed class OdataServiceDocument
{
    [JsonPropertyName("@odata.context")]
    public string ODataContext { get; set; } = "/redfish/v1/$metadata";

    public IReadOnlyList<OdataServiceEntry> Value { get; set; } = [];
}

/// <summary>A single entry in an <see cref="OdataServiceDocument"/>.</summary>
public sealed class OdataServiceEntry
{
    public required string Name { get; set; }

    /// <summary>OData kind — Redfish top-level resources are <c>Singleton</c>s.</summary>
    public string Kind { get; set; } = "Singleton";

    public required string Url { get; set; }
}
