using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedfishEmulator.Api.Auth;

/// <summary>
/// JSON options matching the controllers' Redfish conventions (PascalCase, string
/// enums, omit nulls), for the few places that write JSON outside MVC — e.g. the
/// authentication handler emitting a Redfish error body on a 401 challenge.
/// </summary>
internal static class RedfishJson
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
