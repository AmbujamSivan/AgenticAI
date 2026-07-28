using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RedfishEmulator.StressTests;

/// <summary>Test helpers for authenticating against the emulator's protected endpoints.</summary>
internal static class TestAuth
{
    /// <summary>A client pre-loaded with the demo admin Basic credentials.</summary>
    public static HttpClient AuthedClient(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:admin")));
        return client;
    }
}
