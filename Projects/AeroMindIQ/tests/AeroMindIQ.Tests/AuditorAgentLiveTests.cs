using AeroMindIQ.Agents;

namespace AeroMindIQ.Tests;

/// <summary>
/// Opt-in tests requiring the local Postgres from db/docker-compose.yml to be running
/// (docker compose up in db/). Excluded from the default `dotnet test` run — use
/// `dotnet test --filter Category=Live` to include them. No LLM cost here (Auditor is
/// pure SQL), but it's still an external-infra dependency a bare checkout won't have.
///
/// Because the seed data's "recent" window is relative to whenever it was seeded (see
/// db/seed.sql), these assert structural correctness (no exception, well-formed results)
/// rather than "exactly one specific anomaly," since the seeded anomaly ages out of the
/// Auditor's 3-hour detection window a few hours after seeding.
/// </summary>
[Trait("Category", "Live")]
public class AuditorAgentLiveTests
{
    [Fact]
    public async Task CheckForAnomaliesAsync_AgainstLocalDb_ReturnsWellFormedResultsAsync()
    {
        var anomalies = await AuditorAgent.CheckForAnomaliesAsync(LiveTestDb.AdminConnectionString);

        foreach (var anomaly in anomalies)
        {
            Assert.True(anomaly.LineId > 0);
            Assert.True(anomaly.ZScore >= 3.0m, "CheckForAnomaliesAsync should only return rows at/above its own 3-sigma threshold.");
            Assert.Equal("ZScore", anomaly.TriggerSource);
        }
    }

    [Fact]
    public async Task CheckForAnomaliesViaModelAsync_ScoringServiceUnreachable_FallsBackToZScoreAsync()
    {
        // Points at a port nothing is listening on, exercising the resilience path
        // (confirmed live during Phase 2 verification) without needing the real ML
        // service running for this specific test.
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:1"), Timeout = TimeSpan.FromSeconds(2) };
        var scoringClient = new MlScoringClient(httpClient);

        var anomalies = await AuditorAgent.CheckForAnomaliesViaModelAsync(LiveTestDb.AdminConnectionString, scoringClient);

        foreach (var anomaly in anomalies)
            Assert.Equal("ZScore", anomaly.TriggerSource);
    }
}
