using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.State;

namespace RedfishEmulator.ContractTests;

/// <summary>
/// Contract tests for the Phase 4 diagnostics flow: the RunDiagnostics action,
/// the async task lifecycle, and end-to-end failure detection.
/// </summary>
public sealed class DiagnosticsContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string RunDiagnostics =
        "/redfish/v1/Systems/1/Actions/Oem/RedfishEmulator.RunDiagnostics";

    private readonly WebApplicationFactory<Program> _factory;

    public DiagnosticsContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task System_advertises_the_run_diagnostics_action()
    {
        var client = _factory.AuthedClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/redfish/v1/Systems/1"));
        var target = doc.RootElement
            .GetProperty("Actions").GetProperty("Oem")
            .GetProperty("#RedfishEmulator.RunDiagnostics").GetProperty("target").GetString();

        Assert.Equal(RunDiagnostics, target);
    }

    [Fact]
    public async Task RunDiagnostics_returns_202_with_task_location()
    {
        var client = _factory.AuthedClient();

        var response = await client.PostAsync(RunDiagnostics, content: null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.StartsWith("/redfish/v1/TaskService/Tasks/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Triggered_task_is_pollable_and_reports_all_components_healthy()
    {
        var client = _factory.AuthedClient();

        var post = await client.PostAsync(RunDiagnostics, content: null);
        var taskUrl = post.Headers.Location!.OriginalString;

        var task = await client.GetFromJsonAsync<RedfishTask>(taskUrl);

        Assert.Equal(TaskState.Completed, task!.TaskState);
        Assert.Equal(Health.OK, task.TaskStatus);
        Assert.Contains(task.Messages, m => m.Text.Contains("18 passed"));   // full seed inventory
    }

    [Fact]
    public async Task TaskService_lists_triggered_tasks()
    {
        var client = _factory.AuthedClient();
        await client.PostAsync(RunDiagnostics, content: null);

        var collection = await client.GetFromJsonAsync<ResourceCollection>(
            "/redfish/v1/TaskService/Tasks");

        Assert.True(collection!.MembersODataCount >= 1);
    }

    [Fact]
    public async Task RunDiagnostics_on_unknown_system_returns_404()
    {
        var client = _factory.AuthedClient();

        var response = await client.PostAsync(
            "/redfish/v1/Systems/99/Actions/Oem/RedfishEmulator.RunDiagnostics", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Injected_fault_is_detected_end_to_end()
    {
        // Fresh host so the mutation to shared component state doesn't leak into other tests.
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.AuthedClient();

        // Degrade a GPU directly in the live repository (Phase 5 will do this via fault profiles).
        var repository = factory.Services.GetRequiredService<IComponentRepository>();
        repository.Processors.First(p => p.ProcessorType == ProcessorType.GPU)
            .Status.Health = Health.Critical;

        var post = await client.PostAsync(RunDiagnostics, content: null);
        var task = await client.GetFromJsonAsync<RedfishTask>(post.Headers.Location!.OriginalString);

        Assert.Equal(Health.Critical, task!.TaskStatus);
        Assert.Contains(task.Messages, m => m.MessageId == "RedfishEmulator.1.0.ComponentFailed");
    }
}
