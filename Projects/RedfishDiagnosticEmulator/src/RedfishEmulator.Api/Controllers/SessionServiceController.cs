using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RedfishEmulator.Api.Auth;
using RedfishEmulator.Core.Models;
using RedfishEmulator.Core.Models.Common;
using RedfishEmulator.Core.Services;

namespace RedfishEmulator.Api.Controllers;

/// <summary>
/// Serves the Redfish SessionService and the Sessions collection — the login/logout
/// surface. Creating a session (login) is anonymous; everything else requires auth.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class SessionServiceController(ISessionStore sessions, RedfishCredentials credentials)
    : ControllerBase
{
    private const string Base = "/redfish/v1/SessionService";

    [HttpGet(Base)]
    public SessionServiceResource GetSessionService() => new()
    {
        ODataId = Base,
        ODataType = "#SessionService.v1_1_9.SessionService",
        ODataContext = "/redfish/v1/$metadata#SessionService.SessionService",
        Id = "SessionService",
        Name = "Session Service",
        Sessions = new NavigationLink($"{Base}/Sessions"),
    };

    [HttpGet(Base + "/Sessions")]
    public ResourceCollection GetSessions() => ResourceCollection.Of(
        $"{Base}/Sessions", "SessionCollection", "Session Collection",
        sessions.All().Select(s => s.ODataId));

    /// <summary>Creates a session (login). Anonymous — this is how a client obtains a token.</summary>
    [AllowAnonymous]
    [HttpPost(Base + "/Sessions")]
    public ActionResult<Session> CreateSession([FromBody] CreateSessionRequest request)
    {
        if (!credentials.Validate(request.UserName, request.Password))
        {
            return Unauthorized(RedfishError.AuthenticationFailed());
        }

        var (session, token) = sessions.Create(request.UserName!);

        Response.Headers["X-Auth-Token"] = token;
        Response.Headers.Location = session.ODataId;
        return Created(session.ODataId, session);
    }

    [HttpGet(Base + "/Sessions/{sessionId}")]
    public ActionResult<Session> GetSession(string sessionId) =>
        sessions.GetById(sessionId) is { } session ? session : NotFound();

    /// <summary>Deletes a session (logout).</summary>
    [HttpDelete(Base + "/Sessions/{sessionId}")]
    public IActionResult DeleteSession(string sessionId) =>
        sessions.Delete(sessionId) ? NoContent() : NotFound();
}
