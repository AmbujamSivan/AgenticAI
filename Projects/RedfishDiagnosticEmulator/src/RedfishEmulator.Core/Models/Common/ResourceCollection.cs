using System.Text.Json.Serialization;

namespace RedfishEmulator.Core.Models.Common;

/// <summary>
/// The standard shape of a Redfish collection resource (e.g.
/// <c>ComputerSystemCollection</c>, <c>ProcessorCollection</c>). Members are
/// listed as navigation links — a client follows each <c>@odata.id</c> to fetch
/// the full member resource. The <c>Members@odata.count</c> annotation always
/// reflects the number of members returned.
/// </summary>
public sealed class ResourceCollection : ResourceBase
{
    /// <summary>The member resources, each expressed as a navigation link.</summary>
    [JsonPropertyOrder(10)]
    public IReadOnlyList<NavigationLink> Members { get; set; } = [];

    /// <summary>Count of <see cref="Members"/>, per the Redfish collection contract.</summary>
    [JsonPropertyName("Members@odata.count")]
    [JsonPropertyOrder(9)]
    public int MembersODataCount => Members.Count;

    /// <summary>
    /// Builds a collection resource from the URIs of its members.
    /// </summary>
    /// <param name="odataId">Canonical URI of the collection, e.g. <c>/redfish/v1/Systems</c>.</param>
    /// <param name="collectionType">
    /// Schema type token, e.g. <c>ComputerSystemCollection</c>. Expanded to the full
    /// Redfish form <c>#ComputerSystemCollection.ComputerSystemCollection</c>.
    /// </param>
    /// <param name="name">Human-readable collection name.</param>
    /// <param name="memberIds">Canonical URIs of the member resources.</param>
    public static ResourceCollection Of(
        string odataId,
        string collectionType,
        string name,
        IEnumerable<string> memberIds) => new()
    {
        ODataId = odataId,
        ODataType = $"#{collectionType}.{collectionType}",
        ODataContext = $"/redfish/v1/$metadata#{collectionType}.{collectionType}",
        Name = name,
        Members = memberIds.Select(id => new NavigationLink(id)).ToList(),
    };
}
