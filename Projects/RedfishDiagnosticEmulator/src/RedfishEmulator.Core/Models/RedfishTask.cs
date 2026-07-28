using System.Text.Json.Serialization;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Models;

/// <summary>Lifecycle state of a Redfish <c>Task</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskState
{
    New,
    Running,
    Completed,
    Exception,
    Cancelled,
}

/// <summary>
/// A Redfish <c>Task</c> — an asynchronous operation (here, a diagnostic run). A
/// client that triggers an async action polls this resource until <see cref="TaskState"/>
/// reaches a terminal value, reading <see cref="Messages"/> for the outcome.
/// </summary>
/// <remarks>Named <c>RedfishTask</c> to avoid colliding with <see cref="System.Threading.Tasks.Task"/>.</remarks>
public sealed class RedfishTask : ResourceBase
{
    public TaskState TaskState { get; set; } = TaskState.New;

    /// <summary>Overall completion health (Redfish <c>TaskStatus</c>: OK/Warning/Critical).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Health TaskStatus { get; set; } = Health.OK;

    public int PercentComplete { get; set; }

    public DateTimeOffset StartTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? EndTime { get; set; }

    public IReadOnlyList<Message> Messages { get; set; } = [];

    /// <summary>Vendor extension payload — carries the structured diagnostic report.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Oem { get; set; }
}

/// <summary>A Redfish <c>Message</c> object attached to a task or response.</summary>
public sealed class Message
{
    public required string MessageId { get; set; }

    [JsonPropertyName("Message")]
    public required string Text { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Health Severity { get; set; } = Health.OK;
}
