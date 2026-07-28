using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The Redfish <c>SessionService</c> at <c>/redfish/v1/SessionService</c> — manages
/// authenticated sessions and links to the Sessions collection.
/// </summary>
public sealed class SessionServiceResource : ResourceBase
{
    public bool ServiceEnabled { get; set; } = true;

    /// <summary>Idle session timeout, in seconds.</summary>
    public int SessionTimeout { get; set; } = 1800;

    public Status Status { get; set; } = Status.Ok();

    public NavigationLink? Sessions { get; set; }
}

/// <summary>Request body for creating (logging in) a session.</summary>
public sealed class CreateSessionRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}
