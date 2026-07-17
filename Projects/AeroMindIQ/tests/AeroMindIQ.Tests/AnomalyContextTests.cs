using AeroMindIQ.Agents;

namespace AeroMindIQ.Tests;

public class AnomalyContextTests
{
    [Fact]
    public void Describe_ZScoreTrigger_MentionsBaselineAndZScore()
    {
        var anomaly = new AnomalyContext(
            LineId: 2,
            WindowStart: new DateTime(2026, 7, 17, 3, 0, 0, DateTimeKind.Utc),
            WindowEnd: new DateTime(2026, 7, 17, 4, 0, 0, DateTimeKind.Utc),
            ObservedYieldPct: 68.5m,
            BaselineMeanYieldPct: 97.4m,
            BaselineStdDevYieldPct: 2.4m,
            ZScore: 12.04m);

        var description = anomaly.Describe();

        Assert.Contains("Line 2", description);
        Assert.Contains("68.50%", description);
        Assert.Contains("97.40%", description);
        Assert.Contains("12.04", description);
        Assert.Equal("ZScore", anomaly.TriggerSource);
    }

    [Fact]
    public void Describe_IsolationForestTrigger_MentionsModelScoreAndFeatures()
    {
        var anomaly = new AnomalyContext(
            LineId: 2,
            WindowStart: new DateTime(2026, 7, 17, 3, 0, 0, DateTimeKind.Utc),
            WindowEnd: new DateTime(2026, 7, 17, 3, 0, 0, DateTimeKind.Utc),
            ObservedYieldPct: 69.82m,
            BaselineMeanYieldPct: 0,
            BaselineStdDevYieldPct: 0,
            ZScore: 0,
            TriggerSource: "IsolationForest",
            ModelScore: -0.1432m,
            CycleTimeSec: 84.43m,
            TemperatureVariance: 7.97m);

        var description = anomaly.Describe();

        Assert.Contains("Isolation Forest", description);
        Assert.Contains("-0.1432", description);
        Assert.Contains("84.43", description);
        Assert.Contains("7.97", description);
        Assert.DoesNotContain("Z-score:", description);
    }
}
