namespace AeroMindIQ.Agents;

/// <summary>
/// What the Auditor detected, handed off to the Reviewer/Fetcher/Reporter agents.
/// TriggerSource distinguishes the deterministic 3-sigma path from the Isolation Forest
/// path; the ML-only fields are null when ZScore-triggered and vice versa.
/// </summary>
public sealed record AnomalyContext(
    int LineId,
    DateTime WindowStart,
    DateTime WindowEnd,
    decimal ObservedYieldPct,
    decimal BaselineMeanYieldPct,
    decimal BaselineStdDevYieldPct,
    decimal ZScore,
    string TriggerSource = "ZScore",
    decimal? ModelScore = null,
    decimal? CycleTimeSec = null,
    decimal? TemperatureVariance = null)
{
    public string Describe()
    {
        if (TriggerSource == "IsolationForest")
        {
            return $"Line {LineId}: Isolation Forest flagged a multi-dimensional outlier at " +
                   $"{WindowStart:u} (decision score {ModelScore:F4}; more negative means more " +
                   $"anomalous). Observed: yield {ObservedYieldPct:F2}%, cycle time " +
                   $"{CycleTimeSec:F2}s, temperature variance {TemperatureVariance:F2} — flagged " +
                   $"as a combination, not necessarily because any single metric alone crossed " +
                   $"a fixed threshold.";
        }

        return $"Line {LineId}: yield dropped to {ObservedYieldPct:F2}% during " +
               $"{WindowStart:u}–{WindowEnd:u}, vs a trailing 7-day baseline of " +
               $"{BaselineMeanYieldPct:F2}% (stddev {BaselineStdDevYieldPct:F2}). " +
               $"Z-score: {ZScore:F2} (flag threshold: 3.0).";
    }
}
