using System.Text.Json.Serialization;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>
/// The Redfish error payload returned on a failed request. Wraps an
/// <see cref="RedfishErrorBody"/> under the <c>error</c> key, itself carrying the
/// extended-info message array that describes what went wrong and how to resolve it.
/// </summary>
public sealed class RedfishError
{
    [JsonPropertyName("error")]
    public required RedfishErrorBody Error { get; set; }

    private static RedfishError Of(
        string code, string messageId, string message, Health severity, string? resolution) => new()
    {
        Error = new RedfishErrorBody
        {
            Code = code,
            Message = message,
            ExtendedInfo =
            [
                new ExtendedInfoMessage
                {
                    MessageId = messageId,
                    Message = message,
                    Severity = severity,
                    Resolution = resolution,
                },
            ],
        },
    };

    public static RedfishError ResourceNotFound(string resource) => Of(
        "Base.1.0.ResourceNotFound", "Base.1.0.ResourceNotFound",
        $"The requested resource '{resource}' was not found.", Health.Critical,
        "Provide a valid resource identifier and resubmit the request.");

    public static RedfishError AuthenticationRequired() => Of(
        "Base.1.0.NoValidSession", "Base.1.0.NoValidSession",
        "There is no valid session or credential established with the service.", Health.Critical,
        "Establish a session or supply valid credentials, then resubmit the request.");

    public static RedfishError AuthenticationFailed() => Of(
        "Base.1.0.InsufficientPrivilege", "Base.1.0.InsufficientPrivilege",
        "The supplied credentials could not be authenticated.", Health.Critical,
        "Supply valid credentials and resubmit the request.");
}

/// <summary>The <c>error</c> object of a <see cref="RedfishError"/>.</summary>
public sealed class RedfishErrorBody
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("@Message.ExtendedInfo")]
    public required IReadOnlyList<ExtendedInfoMessage> ExtendedInfo { get; set; }
}

/// <summary>A single Redfish message describing an error condition.</summary>
public sealed class ExtendedInfoMessage
{
    public required string MessageId { get; set; }

    public required string Message { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Health Severity { get; set; } = Health.Critical;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Resolution { get; set; }
}
