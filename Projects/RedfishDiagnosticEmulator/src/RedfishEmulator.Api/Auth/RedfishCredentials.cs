namespace RedfishEmulator.Api.Auth;

/// <summary>
/// Validates account credentials for the emulator. A single admin account is used;
/// its username/password default to <c>admin</c>/<c>admin</c> and are overridable via
/// configuration (<c>Redfish:AdminUsername</c> / <c>Redfish:AdminPassword</c>, e.g.
/// the <c>Redfish__AdminUsername</c> environment variable).
/// </summary>
/// <remarks>Demo credentials for a mock BMC — not a production identity store.</remarks>
public sealed class RedfishCredentials(IConfiguration configuration)
{
    public string AdminUsername => configuration["Redfish:AdminUsername"] ?? "admin";

    private string AdminPassword => configuration["Redfish:AdminPassword"] ?? "admin";

    public bool Validate(string? userName, string? password) =>
        !string.IsNullOrEmpty(userName) &&
        string.Equals(userName, AdminUsername, StringComparison.Ordinal) &&
        string.Equals(password, AdminPassword, StringComparison.Ordinal);
}
