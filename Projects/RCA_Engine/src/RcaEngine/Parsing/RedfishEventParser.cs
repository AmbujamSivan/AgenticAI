using System.Text.Json;

namespace RcaEngine.Parsing;

public sealed class RedfishEvent
{
    public string Id { get; init; } = "";
    public DateTimeOffset Created { get; init; }
    public string Severity { get; init; } = "OK";          // OK | Warning | Critical
    public string MessageId { get; init; } = "";           // e.g. "Memory.1.0.CorrectableECCError"
    public string Message { get; init; } = "";
    public string OriginOfCondition { get; init; } = "";   // Redfish resource path of the component
}

/// <summary>Parses a mock Redfish LogEntryCollection (DMTF LogEntry schema subset).</summary>
public static class RedfishEventParser
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<RedfishEvent> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Members", out var members) || members.ValueKind != JsonValueKind.Array)
            return [];

        var events = new List<RedfishEvent>();
        foreach (var member in members.EnumerateArray())
        {
            events.Add(new RedfishEvent
            {
                Id = GetString(member, "Id"),
                Created = member.TryGetProperty("Created", out var created) && created.TryGetDateTimeOffset(out var dto)
                    ? dto : default,
                Severity = GetString(member, "Severity", "OK"),
                MessageId = GetString(member, "MessageId"),
                Message = GetString(member, "Message"),
                OriginOfCondition = member.TryGetProperty("OriginOfCondition", out var origin)
                    ? origin.ValueKind switch
                    {
                        JsonValueKind.String => origin.GetString() ?? "",
                        JsonValueKind.Object => origin.TryGetProperty("@odata.id", out var odataId) ? odataId.GetString() ?? "" : "",
                        _ => ""
                    }
                    : ""
            });
        }
        return events.OrderBy(e => e.Created).ToList();
    }

    private static string GetString(JsonElement element, string name, string fallback = "") =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? fallback
            : fallback;
}
