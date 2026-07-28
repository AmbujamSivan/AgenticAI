using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.ContractTests;

/// <summary>
/// Contract tests for the OEM fault-injection flow and its end-to-end effect on
/// diagnostics and telemetry. Each test uses its own host so injected faults never
/// leak between tests.
/// </summary>
public sealed class FaultInjectionContractTests
{
    private const string FaultBase = "/redfish/v1/Oem/RedfishEmulator/FaultInjection";
    private const string RunDiagnostics =
        "/redfish/v1/Systems/1/Actions/Oem/RedfishEmulator.RunDiagnostics";

    [Fact]
    public async Task ServiceRoot_advertises_the_fault_injection_endpoint()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/redfish/v1"));
        var link = doc.RootElement.GetProperty("Oem").GetProperty("RedfishEmulator")
            .GetProperty("FaultInjection").GetProperty("@odata.id").GetString();

        Assert.Equal(FaultBase, link);
    }

    [Fact]
    public async Task Fault_listing_reports_four_inactive_profiles()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync(FaultBase));
        var profiles = doc.RootElement.GetProperty("Profiles");

        Assert.Equal(4, profiles.GetArrayLength());
        Assert.All(profiles.EnumerateArray(), p => Assert.False(p.GetProperty("Active").GetBoolean()));
    }

    [Fact]
    public async Task Injected_gpu_fault_fails_diagnostics_then_clears_back_to_healthy()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Inject: GPU falls off the bus.
        await client.PostAsync($"{FaultBase}/GpuOffBus/Activate", content: null);

        var faulted = await RunDiagnosticsTask(client);
        Assert.Equal(Health.Critical, faulted.TaskStatus);
        Assert.Contains(faulted.Messages, m => m.MessageId == "RedfishEmulator.1.0.ComponentFailed");

        // Clear: platform returns to health and diagnostics pass again.
        await client.PostAsync($"{FaultBase}/GpuOffBus/Clear", content: null);

        var recovered = await RunDiagnosticsTask(client);
        Assert.Equal(Health.OK, recovered.TaskStatus);
    }

    [Fact]
    public async Task Gpu_fault_surfaces_on_the_component_and_system_rollup()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        await client.PostAsync($"{FaultBase}/GpuOffBus/Activate", content: null);

        var gpu = await client.GetFromJsonAsync<Processor>("/redfish/v1/Systems/1/Processors/GPU1");
        Assert.Equal(Health.Critical, gpu!.Status.Health);
        Assert.Equal(ResourceState.UnavailableOffline, gpu.Status.State);

        var system = await client.GetFromJsonAsync<ComputerSystem>("/redfish/v1/Systems/1");
        Assert.Equal(Health.Critical, system!.Status.HealthRollup);
    }

    [Fact]
    public async Task Thermal_trip_pushes_a_cpu_sensor_over_its_critical_threshold()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        await client.PostAsync($"{FaultBase}/ThermalTrip/Activate", content: null);

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/redfish/v1/Chassis/1/Thermal"));
        var cpuSensors = doc.RootElement.GetProperty("Temperatures").EnumerateArray()
            .Where(t => t.GetProperty("PhysicalContext").GetString() == "CPU")
            .ToList();

        Assert.Contains(cpuSensors, t =>
            t.GetProperty("Status").GetProperty("Health").GetString() == "Critical" &&
            t.GetProperty("ReadingCelsius").GetDouble() >=
                t.GetProperty("UpperThresholdCritical").GetDouble());
    }

    [Fact]
    public async Task Reset_clears_all_faults()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        await client.PostAsync($"{FaultBase}/GpuOffBus/Activate", content: null);
        await client.PostAsync($"{FaultBase}/MemoryEcc/Activate", content: null);

        var reset = await client.PostAsync($"{FaultBase}/Reset", content: null);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var system = await client.GetFromJsonAsync<ComputerSystem>("/redfish/v1/Systems/1");
        Assert.Equal(Health.OK, system!.Status.Health);
    }

    [Fact]
    public async Task Unknown_profile_returns_404()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"{FaultBase}/NoSuchProfile/Activate", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<RedfishTask> RunDiagnosticsTask(HttpClient client)
    {
        var post = await client.PostAsync(RunDiagnostics, content: null);
        return (await client.GetFromJsonAsync<RedfishTask>(post.Headers.Location!.OriginalString))!;
    }
}
