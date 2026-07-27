using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RedfishEmulator.ContractTests;

/// <summary>
/// Phase 0 smoke/contract tests: spin up the real API host in-memory and assert
/// the Redfish service entry points return spec-shaped responses.
/// </summary>
public sealed class ServiceRootContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServiceRootContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Redfish_version_map_lists_v1()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/redfish");
        response.EnsureSuccessStatusCode();

        var versions = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(versions);
        Assert.Equal("/redfish/v1/", versions!["v1"]);
    }

    [Fact]
    public async Task ServiceRoot_returns_ok_with_required_odata_annotations()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/redfish/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        // Every Redfish resource must carry these annotations.
        Assert.Equal("/redfish/v1", root.GetProperty("@odata.id").GetString());
        Assert.StartsWith("#ServiceRoot.", root.GetProperty("@odata.type").GetString());
        Assert.Equal("RootService", root.GetProperty("Id").GetString());
    }

    [Fact]
    public async Task ServiceRoot_exposes_navigation_links_to_core_collections()
    {
        var client = _factory.CreateClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/redfish/v1"));
        var root = doc.RootElement;

        Assert.Equal("/redfish/v1/Systems", root.GetProperty("Systems").GetProperty("@odata.id").GetString());
        Assert.Equal("/redfish/v1/Chassis", root.GetProperty("Chassis").GetProperty("@odata.id").GetString());
        Assert.Equal("/redfish/v1/Managers", root.GetProperty("Managers").GetProperty("@odata.id").GetString());
    }
}
