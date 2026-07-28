using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Auth;

/// <summary>
/// Authenticates Redfish requests by either an <c>X-Auth-Token</c> session token or
/// HTTP Basic credentials — the two mechanisms the Redfish spec defines. Requests
/// with no credentials are left unauthenticated so the authorization layer can allow
/// public resources (ServiceRoot, metadata) and challenge everything else.
/// </summary>
public sealed class RedfishAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionStore sessions,
    RedfishCredentials credentials)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Redfish";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Session token.
        if (Request.Headers.TryGetValue("X-Auth-Token", out var token) &&
            sessions.GetByToken(token.ToString()) is { UserName: { } sessionUser })
        {
            return Task.FromResult(Success(sessionUser));
        }

        // 2. HTTP Basic.
        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var (user, password) = DecodeBasic(authorization["Basic ".Length..]);
            return Task.FromResult(credentials.Validate(user, password)
                ? Success(user!)
                : AuthenticateResult.Fail("Invalid credentials."));
        }

        // 3. No credentials — let authorization decide (public vs protected).
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    /// <summary>Emits a Redfish error body (not an empty 401) when a challenge is issued.</summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Basic realm=\"Redfish\"";
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(RedfishError.AuthenticationRequired(), RedfishJson.Default);
    }

    private AuthenticateResult Success(string userName)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private static (string? User, string? Password) DecodeBasic(string encoded)
    {
        try
        {
            var pair = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var separator = pair.IndexOf(':');
            return separator < 0 ? (null, null) : (pair[..separator], pair[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return (null, null);
        }
    }
}
