using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.Testing;
using RedfishEmulator.Core.Models;

namespace RedfishEmulator.ContractTests;

/// <summary>
/// Contract tests for the OData discovery documents required by the Redfish
/// protocol: <c>/redfish/v1/$metadata</c> and <c>/redfish/v1/odata</c>.
/// </summary>
public sealed class MetadataContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MetadataContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Metadata_returns_wellformed_edmx_xml()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/redfish/v1/$metadata");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);

        // Must parse as XML and be rooted at edmx:Edmx.
        var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Edmx", doc.Root?.Name.LocalName);
    }

    [Fact]
    public async Task Metadata_references_core_schemas()
    {
        var client = _factory.CreateClient();

        var xml = await client.GetStringAsync("/redfish/v1/$metadata");

        Assert.Contains("ServiceRoot_v1.xml", xml);
        Assert.Contains("ComputerSystem_v1.xml", xml);
        Assert.Contains("Processor_v1.xml", xml);
    }

    [Fact]
    public async Task Odata_service_document_lists_top_level_resources()
    {
        var client = _factory.CreateClient();

        var doc = await client.GetFromJsonAsync<OdataServiceDocument>("/redfish/v1/odata");

        Assert.NotNull(doc);
        Assert.Equal("/redfish/v1/$metadata", doc!.ODataContext);
        Assert.Contains(doc.Value, e => e is { Name: "Systems", Url: "/redfish/v1/Systems" });
        Assert.Contains(doc.Value, e => e.Name == "Chassis");
    }
}
