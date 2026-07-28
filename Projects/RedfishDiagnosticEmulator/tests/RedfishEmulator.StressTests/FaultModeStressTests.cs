using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.StressTests;

/// <summary>
/// Stress scenarios that hammer the emulator under an injected fault, asserting the
/// failure mode is reported consistently across many concurrent requests.
/// </summary>
public sealed class FaultModeStressTests
{
    private const string FaultBase = "/redfish/v1/Oem/RedfishEmulator/FaultInjection";
    private const string RunDiagnostics =
        "/redfish/v1/Systems/1/Actions/Oem/RedfishEmulator.RunDiagnostics";

    [Fact]
    public async Task Concurrent_diagnostics_under_a_fault_all_report_the_failure()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        await client.PostAsync($"{FaultBase}/GpuOffBus/Activate", content: null);

        // Fire 40 diagnostic runs concurrently; every one must observe the injected fault.
        var runs = await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ =>
        {
            var post = await client.PostAsync(RunDiagnostics, content: null);
            return await client.GetFromJsonAsync<RedfishTask>(post.Headers.Location!.OriginalString);
        }));

        Assert.All(runs, task => Assert.Equal(Health.Critical, task!.TaskStatus));
    }

    [Fact]
    public async Task Concurrent_reads_and_toggles_never_error()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Interleave fault toggles with heavy inventory/telemetry reads.
        var toggles = Enumerable.Range(0, 10).Select(async i =>
        {
            var action = i % 2 == 0 ? "Activate" : "Clear";
            return await client.PostAsync($"{FaultBase}/GpuOffBus/{action}", content: null);
        });

        var reads = Enumerable.Range(0, 40).Select(i => client.GetAsync(i % 2 == 0
            ? "/redfish/v1/Systems/1"
            : "/redfish/v1/Chassis/1/Thermal"));

        var responses = await Task.WhenAll(toggles.Concat<Task<HttpResponseMessage>>(reads));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Leave the platform clean.
        await client.PostAsync($"{FaultBase}/Reset", content: null);
    }
}
