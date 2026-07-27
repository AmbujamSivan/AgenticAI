using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RedfishEmulator.StressTests;

/// <summary>
/// Phase 0 concurrency smoke test. Later phases expand this project into full
/// stress and fault-injection load scenarios against the diagnostic endpoints.
/// </summary>
public sealed class ServiceRootLoadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServiceRootLoadTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ServiceRoot_handles_concurrent_reads()
    {
        var client = _factory.CreateClient();

        var requests = Enumerable
            .Range(0, 50)
            .Select(_ => client.GetAsync("/redfish/v1"));

        var responses = await Task.WhenAll(requests);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }
}
