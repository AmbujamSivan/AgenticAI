using System.Text.Json.Serialization;
using RedfishEmulator.Core.Models.Common;

namespace RedfishEmulator.Core.Diagnostics;

/// <summary>Outcome of a single diagnostic check.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiagnosticOutcome
{
    Pass,
    Warning,
    Fail,
}

/// <summary>The result of diagnosing one component.</summary>
public sealed record DiagnosticResult(
    string Category,
    string ComponentId,
    string? ComponentName,
    DiagnosticOutcome Outcome,
    string Message);

/// <summary>
/// The aggregate result of a diagnostic run across a system's components: every
/// per-component <see cref="DiagnosticResult"/>, the counts, and the overall outcome
/// (the worst individual result).
/// </summary>
public sealed class DiagnosticReport
{
    public required string SystemId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public required IReadOnlyList<DiagnosticResult> Results { get; init; }

    public DiagnosticOutcome Overall => Results.Count == 0
        ? DiagnosticOutcome.Pass
        : Results.Max(r => r.Outcome);

    public int PassedCount => Results.Count(r => r.Outcome == DiagnosticOutcome.Pass);
    public int WarningCount => Results.Count(r => r.Outcome == DiagnosticOutcome.Warning);
    public int FailedCount => Results.Count(r => r.Outcome == DiagnosticOutcome.Fail);

    /// <summary>Maps the overall diagnostic outcome onto a Redfish health value.</summary>
    public Health OverallHealth => Overall switch
    {
        DiagnosticOutcome.Fail => Health.Critical,
        DiagnosticOutcome.Warning => Health.Warning,
        _ => Health.OK,
    };
}
