using System.Text.Json.Serialization;

namespace RedfishEmulator.Core.Models.Common;

/// <summary>
/// Base for every addressable Redfish resource. Carries the OData annotations
/// (<c>@odata.id</c>, <c>@odata.type</c>, <c>@odata.context</c>) that the Redfish
/// specification (DMTF DSP0266) requires on all resources so clients can
/// self-navigate the service and resolve schema types.
/// </summary>
/// <remarks>
/// The negative <see cref="JsonPropertyOrderAttribute"/> values force the OData
/// annotations to serialize ahead of a derived type's own members, matching the
/// conventional Redfish payload shape (annotations first, then <c>Id</c>/<c>Name</c>).
/// </remarks>
public abstract class ResourceBase
{
    /// <summary>Canonical URI of this resource, e.g. <c>/redfish/v1/Systems/1</c>.</summary>
    [JsonPropertyName("@odata.id")]
    [JsonPropertyOrder(-100)]
    public required string ODataId { get; set; }

    /// <summary>
    /// Fully-qualified schema type of this resource,
    /// e.g. <c>#ComputerSystem.v1_20_0.ComputerSystem</c>.
    /// </summary>
    [JsonPropertyName("@odata.type")]
    [JsonPropertyOrder(-99)]
    public required string ODataType { get; set; }

    /// <summary>Optional link to the metadata document describing this resource's schema.</summary>
    [JsonPropertyName("@odata.context")]
    [JsonPropertyOrder(-98)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ODataContext { get; set; }

    /// <summary>Unique identifier of this resource within its collection.</summary>
    [JsonPropertyOrder(-97)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>Human-readable name for this resource.</summary>
    [JsonPropertyOrder(-96)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}
