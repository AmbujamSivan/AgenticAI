namespace AeroMindIQ.Agents;

/// <summary>
/// What the Auditor detected, handed off to the Fetcher and Reporter agents.
/// </summary>
public sealed record AnomalyContext(
    int LineId,
    DateTime WindowStart,
    DateTime WindowEnd,
    decimal ObservedYieldPct,
    decimal BaselineMeanYieldPct,
    decimal BaselineStdDevYieldPct,
    decimal ZScore)
{
    public string Describe() =>
        $"Line {LineId}: yield dropped to {ObservedYieldPct:F2}% during " +
        $"{WindowStart:u}–{WindowEnd:u}, vs a trailing 7-day baseline of " +
        $"{BaselineMeanYieldPct:F2}% (stddev {BaselineStdDevYieldPct:F2}). " +
        $"Z-score: {ZScore:F2} (flag threshold: 3.0).";
}
