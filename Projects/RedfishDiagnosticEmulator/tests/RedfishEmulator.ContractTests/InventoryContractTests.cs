using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.ContractTests;

/// <summary>
/// Contract tests for the Phase 2 inventory APIs, exercised against the real host
/// with the shipped JSON seed data.
/// </summary>
public sealed class InventoryContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InventoryContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Systems_collection_contains_the_seeded_system()
    {
        var client = _factory.CreateClient();

        var collection = await client.GetFromJsonAsync<ResourceCollection>("/redfish/v1/Systems");

        Assert.NotNull(collection);
        Assert.Equal(1, collection!.MembersODataCount);
        Assert.Equal("/redfish/v1/Systems/1", collection.Members[0].ODataId);
    }

    [Fact]
    public async Task System_reports_computed_summaries_and_health()
    {
        var client = _factory.CreateClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/redfish/v1/Systems/1"));
        var root = doc.RootElement;

        Assert.Equal("Dell Inc.", root.GetProperty("Manufacturer").GetString());
        Assert.Equal(2, root.GetProperty("ProcessorSummary").GetProperty("Count").GetInt32());
        Assert.Equal(512, root.GetProperty("MemorySummary").GetProperty("TotalSystemMemoryGiB").GetDouble());
        Assert.Equal("OK", root.GetProperty("Status").GetProperty("HealthRollup").GetString());
    }

    [Fact]
    public async Task Processors_collection_includes_cpus_and_gpu_accelerators()
    {
        var client = _factory.CreateClient();

        var collection = await client.GetFromJsonAsync<ResourceCollection>(
            "/redfish/v1/Systems/1/Processors");

        Assert.Equal(6, collection!.MembersODataCount);   // 2 CPU + 4 GPU
    }

    [Fact]
    public async Task Gpu_processor_is_typed_as_GPU()
    {
        var client = _factory.CreateClient();

        var gpu = await client.GetFromJsonAsync<Processor>(
            "/redfish/v1/Systems/1/Processors/GPU1");

        Assert.Equal(ProcessorType.GPU, gpu!.ProcessorType);
        Assert.Equal("NVIDIA Corporation", gpu.Manufacturer);
    }

    [Theory]
    [InlineData("/redfish/v1/Systems/1/Memory", 8)]
    [InlineData("/redfish/v1/Systems/1/PCIeDevices", 4)]
    public async Task Component_collections_have_expected_counts(string url, int expected)
    {
        var client = _factory.CreateClient();

        var collection = await client.GetFromJsonAsync<ResourceCollection>(url);

        Assert.Equal(expected, collection!.MembersODataCount);
    }

    [Fact]
    public async Task Chassis_is_served_with_type_and_identity()
    {
        var client = _factory.CreateClient();

        var chassis = await client.GetFromJsonAsync<Chassis>("/redfish/v1/Chassis/1");

        Assert.Equal("RackMount", chassis!.ChassisType);
        Assert.Equal("1", chassis.Id);
    }

    [Theory]
    [InlineData("/redfish/v1/Systems/99")]
    [InlineData("/redfish/v1/Systems/1/Processors/CPU9")]
    [InlineData("/redfish/v1/Chassis/99")]
    public async Task Unknown_resources_return_404(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
