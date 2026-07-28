using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// A Redfish <c>Session</c> — an authenticated login. Created by POSTing credentials
/// to the Sessions collection; the returned <c>X-Auth-Token</c> authenticates
/// subsequent requests until the session is deleted (logout).
/// </summary>
public sealed class Session : ResourceBase
{
    /// <summary>The account this session belongs to.</summary>
    public string? UserName { get; set; }
}
