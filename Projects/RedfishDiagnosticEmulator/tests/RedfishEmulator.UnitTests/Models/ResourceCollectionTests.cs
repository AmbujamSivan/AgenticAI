using System.Text.Json;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.UnitTests.Models;

/// <summary>Unit tests for the Redfish <see cref="ResourceCollection"/> shape.</summary>
public sealed class ResourceCollectionTests
{
    [Fact]
    public void Of_expands_member_ids_into_navigation_links()
    {
        var collection = ResourceCollection.Of(
            "/redfish/v1/Systems",
            "ComputerSystemCollection",
            "Computer System Collection",
            ["/redfish/v1/Systems/1", "/redfish/v1/Systems/2"]);

        Assert.Equal("#ComputerSystemCollection.ComputerSystemCollection", collection.ODataType);
        Assert.Equal(2, collection.MembersODataCount);
        Assert.Equal("/redfish/v1/Systems/1", collection.Members[0].ODataId);
    }

    [Fact]
    public void Empty_collection_reports_zero_count()
    {
        var collection = ResourceCollection.Of(
            "/redfish/v1/Systems", "ComputerSystemCollection", "Systems", []);

        Assert.Equal(0, collection.MembersODataCount);
        Assert.Empty(collection.Members);
    }

    [Fact]
    public void Serializes_with_members_count_annotation()
    {
        var collection = ResourceCollection.Of(
            "/redfish/v1/Systems", "ComputerSystemCollection", "Systems",
            ["/redfish/v1/Systems/1"]);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
        var json = JsonSerializer.Serialize(collection, options);

        Assert.Contains("\"Members@odata.count\":1", json);
        // Annotations must precede the Members array in the payload.
        Assert.True(json.IndexOf("@odata.id", StringComparison.Ordinal)
                    < json.IndexOf("\"Members\"", StringComparison.Ordinal));
    }
}
