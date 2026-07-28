using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.ContractTests;

/// <summary>Contract tests for the Phase 3 telemetry APIs against the real host.</summary>
public sealed class TelemetryContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TelemetryContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task TelemetryService_links_to_metric_reports()
    {
        var client = _factory.AuthedClient();

        var service = await client.GetFromJsonAsync<TelemetryService>("/redfish/v1/TelemetryService");

        Assert.Equal("/redfish/v1/TelemetryService/MetricReports", service!.MetricReports!.ODataId);
    }

    [Fact]
    public async Task MetricReports_collection_lists_three_reports()
    {
        var client = _factory.AuthedClient();

        var collection = await client.GetFromJsonAsync<ResourceCollection>(
            "/redfish/v1/TelemetryService/MetricReports");

        Assert.Equal(3, collection!.MembersODataCount);
    }

    [Fact]
    public async Task GpuMetrics_report_carries_values_for_every_gpu()
    {
        var client = _factory.AuthedClient();

        var report = await client.GetFromJsonAsync<MetricReport>(
            "/redfish/v1/TelemetryService/MetricReports/GPUMetrics");

        Assert.Equal(8, report!.MetricValues.Count);   // 4 GPU x (temp + utilization)
    }

    [Fact]
    public async Task Thermal_exposes_gpu_temperature_sensors()
    {
        var client = _factory.AuthedClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/redfish/v1/Chassis/1/Thermal"));
        var temps = doc.RootElement.GetProperty("Temperatures");

        Assert.Equal(7, temps.GetArrayLength());
        Assert.Contains(temps.EnumerateArray(),
            t => t.GetProperty("PhysicalContext").GetString() == "GPU");
    }

    [Fact]
    public async Task Power_reports_consumption_and_supplies()
    {
        var client = _factory.AuthedClient();

        var power = await client.GetFromJsonAsync<Power>("/redfish/v1/Chassis/1/Power");

        Assert.True(power!.PowerControl[0].PowerConsumedWatts > 0);
        Assert.Equal(2, power.PowerSupplies.Count);
    }

    [Fact]
    public async Task Chassis_links_thermal_and_power()
    {
        var client = _factory.AuthedClient();

        var chassis = await client.GetFromJsonAsync<Chassis>("/redfish/v1/Chassis/1");

        Assert.Equal("/redfish/v1/Chassis/1/Thermal", chassis!.Thermal!.ODataId);
        Assert.Equal("/redfish/v1/Chassis/1/Power", chassis.Power!.ODataId);
    }

    [Theory]
    [InlineData("/redfish/v1/TelemetryService/MetricReports/NoSuchReport")]
    [InlineData("/redfish/v1/Chassis/99/Thermal")]
    [InlineData("/redfish/v1/Chassis/99/Power")]
    public async Task Unknown_telemetry_resources_return_404(string url)
    {
        var client = _factory.AuthedClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
