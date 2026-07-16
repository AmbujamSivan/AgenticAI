using Npgsql;

namespace AeroMindIQ.Agents;

/// <summary>
/// Agent A: watches production_runs for anomalies. The primary path,
/// CheckForAnomaliesViaModelAsync, scores recent rows through a scikit-learn Isolation
/// Forest (ml/service.py) over multiple features so combinations that don't trip any
/// single-column threshold still get caught. CheckForAnomaliesAsync (the original
/// 3-sigma yield check) stays as a real, tested fallback if that scoring service is
/// unreachable — not dead code, since a network dependency is a genuine new failure mode.
/// </summary>
public static class AuditorAgent
{
    private const decimal ZScoreThreshold = 3.0m;

    private const string DetectionQuery = """
        WITH baseline AS (
            SELECT line_id, AVG(yield_pct) AS mean_yield, STDDEV_POP(yield_pct) AS stddev_yield
            FROM production_runs
            WHERE started_at < NOW() - INTERVAL '3 hours'
              AND started_at >= NOW() - INTERVAL '7 days'
            GROUP BY line_id
        ),
        recent AS (
            SELECT line_id, AVG(yield_pct) AS recent_yield, MIN(started_at) AS window_start, MAX(started_at) AS window_end
            FROM production_runs
            WHERE started_at >= NOW() - INTERVAL '3 hours'
            GROUP BY line_id
        )
        SELECT r.line_id, r.recent_yield, b.mean_yield, b.stddev_yield, r.window_start, r.window_end,
               (b.mean_yield - r.recent_yield) / NULLIF(b.stddev_yield, 0) AS z_score
        FROM recent r
        JOIN baseline b ON r.line_id = b.line_id
        WHERE b.stddev_yield > 0
        ORDER BY z_score DESC NULLS LAST;
        """;

    private const string RecentFeatureRowsQuery = """
        SELECT line_id, started_at, cycle_time_sec, temperature_variance, units_produced, yield_pct
        FROM production_runs
        WHERE started_at >= NOW() - INTERVAL '3 hours'
        ORDER BY started_at;
        """;

    public static async Task<IReadOnlyList<AnomalyContext>> CheckForAnomaliesViaModelAsync(
        string connectionString,
        MlScoringClient scoringClient)
    {
        IReadOnlyList<ProductionFeatureRow> rows;
        IReadOnlyList<ScoredRow> scored;

        try
        {
            rows = await FetchRecentFeatureRowsAsync(connectionString);
            if (rows.Count == 0)
                return [];

            scored = await scoringClient.ScoreAsync(rows);
        }
        catch (HttpRequestException ex)
        {
            System.Console.Error.WriteLine(
                $"[Auditor] ML scoring service unreachable ({ex.Message}) — falling back to the 3-sigma check.");
            return await CheckForAnomaliesAsync(connectionString);
        }
        catch (TaskCanceledException ex)
        {
            System.Console.Error.WriteLine(
                $"[Auditor] ML scoring service timed out ({ex.Message}) — falling back to the 3-sigma check.");
            return await CheckForAnomaliesAsync(connectionString);
        }

        var anomalies = new List<AnomalyContext>();
        for (var i = 0; i < rows.Count && i < scored.Count; i++)
        {
            if (!scored[i].IsAnomaly)
                continue;

            var row = rows[i];
            anomalies.Add(new AnomalyContext(
                LineId: row.LineId,
                WindowStart: row.StartedAt,
                WindowEnd: row.StartedAt,
                ObservedYieldPct: row.YieldPct,
                BaselineMeanYieldPct: 0,
                BaselineStdDevYieldPct: 0,
                ZScore: 0,
                TriggerSource: "IsolationForest",
                ModelScore: scored[i].Score,
                CycleTimeSec: row.CycleTimeSec,
                TemperatureVariance: row.TemperatureVariance));
        }

        // sklearn's decision_function: more negative = more anomalous.
        return anomalies.OrderBy(a => a.ModelScore).ToList();
    }

    public static async Task<IReadOnlyList<AnomalyContext>> CheckForAnomaliesAsync(string connectionString)
    {
        var anomalies = new List<AnomalyContext>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(DetectionQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(6))
                continue;

            var zScore = reader.GetDecimal(6);
            if (zScore < ZScoreThreshold)
                continue;

            anomalies.Add(new AnomalyContext(
                LineId: reader.GetInt32(0),
                ObservedYieldPct: reader.GetDecimal(1),
                BaselineMeanYieldPct: reader.GetDecimal(2),
                BaselineStdDevYieldPct: reader.GetDecimal(3),
                WindowStart: reader.GetDateTime(4),
                WindowEnd: reader.GetDateTime(5),
                ZScore: zScore));
        }

        return anomalies;
    }

    private static async Task<IReadOnlyList<ProductionFeatureRow>> FetchRecentFeatureRowsAsync(string connectionString)
    {
        var rows = new List<ProductionFeatureRow>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(RecentFeatureRowsQuery, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new ProductionFeatureRow(
                LineId: reader.GetInt32(0),
                StartedAt: reader.GetDateTime(1),
                CycleTimeSec: reader.GetDecimal(2),
                TemperatureVariance: reader.GetDecimal(3),
                UnitsProduced: reader.GetInt32(4),
                YieldPct: reader.GetDecimal(5)));
        }

        return rows;
    }
}
