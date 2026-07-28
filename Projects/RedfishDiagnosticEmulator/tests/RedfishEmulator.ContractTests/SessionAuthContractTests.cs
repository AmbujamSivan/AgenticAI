using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RedfishEmulator.ContractTests;

/// <summary>
/// Contract tests for Phase 6 authentication: the public/protected split, HTTP Basic
/// and session-token auth, the session lifecycle, and Redfish error bodies.
/// </summary>
public sealed class SessionAuthContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ProtectedResource = "/redfish/v1/Systems/1";
    private const string SessionsUrl = "/redfish/v1/SessionService/Sessions";

    private readonly WebApplicationFactory<Program> _factory;

    public SessionAuthContractTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("/redfish")]
    [InlineData("/redfish/v1")]
    [InlineData("/redfish/v1/$metadata")]
    public async Task Public_resources_are_reachable_without_credentials(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_resource_without_credentials_returns_401_redfish_error()
    {
        var response = await _factory.CreateClient().GetAsync(ProtectedResource);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("Base.1.0.NoValidSession", error.GetProperty("code").GetString());
        Assert.NotEmpty(error.GetProperty("@Message.ExtendedInfo").EnumerateArray());
    }

    [Fact]
    public async Task Basic_auth_with_valid_credentials_succeeds()
    {
        var response = await _factory.AuthedClient().GetAsync(ProtectedResource);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Basic_auth_with_wrong_password_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:wrong")));

        var response = await client.GetAsync(ProtectedResource);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_login_yields_a_token_that_authenticates_then_logout_revokes_it()
    {
        var client = _factory.CreateClient();

        // Login (anonymous) → 201 + X-Auth-Token.
        var login = await client.PostAsync(SessionsUrl,
            JsonContent.Create(new { UserName = "admin", Password = "admin" }));
        Assert.Equal(HttpStatusCode.Created, login.StatusCode);
        var token = Assert.Single(login.Headers.GetValues("X-Auth-Token"));

        // The token authenticates a protected request.
        var sessionUrl = login.Headers.Location!.OriginalString;
        client.DefaultRequestHeaders.Add("X-Auth-Token", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ProtectedResource)).StatusCode);

        // Logout deletes the session…
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(sessionUrl)).StatusCode);

        // …and the token no longer works.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ProtectedResource)).StatusCode);
    }

    [Fact]
    public async Task Session_login_with_wrong_credentials_is_rejected()
    {
        var response = await _factory.CreateClient().PostAsync(SessionsUrl,
            JsonContent.Create(new { UserName = "admin", Password = "nope" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Not_found_returns_a_redfish_error_body()
    {
        var response = await _factory.AuthedClient().GetAsync("/redfish/v1/Systems/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Base.1.0.ResourceNotFound",
            doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
