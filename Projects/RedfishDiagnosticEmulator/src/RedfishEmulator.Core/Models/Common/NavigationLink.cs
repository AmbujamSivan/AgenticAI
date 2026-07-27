using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace RedfishEmulator.Core.Models.Common;

/// <summary>
/// A Redfish reference to another resource: an object carrying a single
/// <c>@odata.id</c> property. Used for the link properties on a resource
/// (e.g. a ServiceRoot's <c>Systems</c> pointing at <c>/redfish/v1/Systems</c>).
/// </summary>
public sealed class NavigationLink
{
    public NavigationLink() { }

    [SetsRequiredMembers]
    public NavigationLink(string oDataId) => ODataId = oDataId;

    /// <summary>URI of the referenced resource.</summary>
    [JsonPropertyName("@odata.id")]
    public required string ODataId { get; set; }
}
