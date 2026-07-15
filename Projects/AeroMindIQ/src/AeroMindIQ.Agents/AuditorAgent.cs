using Npgsql;

namespace AeroMindIQ.Agents;

/// <summary>
/// Agent A: watches production_runs for a yield drop of more than 3 standard deviations
/// from its trailing 7-day baseline. Deterministic, no LLM call — cheap and reliable
/// enough to run every cycle before spending tokens on investigation.
///
/// Follow-up milestone: replace this single-column z-score rule with a trained
/// Isolation Forest / One-Class SVM over multiple features (cycle time, temperature
/// variance, item volume) for genuine multi-dimensional outlier detection.
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
}
