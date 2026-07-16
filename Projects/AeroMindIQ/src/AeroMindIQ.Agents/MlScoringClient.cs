using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AeroMindIQ.Agents;

public sealed record ProductionFeatureRow(
    int LineId,
    DateTime StartedAt,
    decimal CycleTimeSec,
    decimal TemperatureVariance,
    decimal UnitsProduced,
    decimal YieldPct);

public sealed record ScoredRow(bool IsAnomaly, decimal Score);

/// <summary>
/// Thin HTTP client for the Python Isolation Forest scoring service (ml/service.py).
/// Rows are correlated to scores by list position — the service preserves request order —
/// rather than by re-parsing timestamps round-tripped through JSON.
/// </summary>
public sealed class MlScoringClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<ScoredRow>> ScoreAsync(
        IReadOnlyList<ProductionFeatureRow> rows,
        CancellationToken cancellationToken = default)
    {
        var payload = rows.Select(r => new ScoreRequestRow(
            r.LineId,
            r.StartedAt.ToString("O"),
            r.CycleTimeSec,
            r.TemperatureVariance,
            r.UnitsProduced,
            r.YieldPct));

        using var response = await httpClient.PostAsJsonAsync("/score", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var scored = await response.Content.ReadFromJsonAsync<List<ScoreResponseRow>>(cancellationToken)
            ?? [];

        return scored.Select(s => new ScoredRow(s.IsAnomaly, (decimal)s.Score)).ToList();
    }

    private sealed record ScoreRequestRow(
        [property: JsonPropertyName("line_id")] int LineId,
        [property: JsonPropertyName("started_at")] string StartedAt,
        [property: JsonPropertyName("cycle_time_sec")] decimal CycleTimeSec,
        [property: JsonPropertyName("temperature_variance")] decimal TemperatureVariance,
        [property: JsonPropertyName("units_produced")] decimal UnitsProduced,
        [property: JsonPropertyName("yield_pct")] decimal YieldPct);

    private sealed record ScoreResponseRow(
        [property: JsonPropertyName("line_id")] int LineId,
        [property: JsonPropertyName("started_at")] string StartedAt,
        [property: JsonPropertyName("is_anomaly")] bool IsAnomaly,
        [property: JsonPropertyName("score")] double Score);
}
