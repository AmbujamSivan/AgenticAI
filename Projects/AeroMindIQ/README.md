# AeroMind IQ

A multi-agent backend that watches a live manufacturing database for yield anomalies and
coordinates a squad of Semantic Kernel agents to investigate and draft a root-cause report.

This is the first milestone: an end-to-end happy path (Auditor → Fetcher → Reporter), no
Reviewer loop and no full observability stack yet. See "Follow-up milestones" below.

## Architecture

- **Agent A — Auditor** (`AeroMindIQ.Agents/AuditorAgent.cs`): deterministic SQL check, no LLM
  call. Flags a yield drop of more than 3 standard deviations from a trailing 7-day baseline.
- **Agent B — Fetcher** (`FetcherAgent.cs` + `DatabasePlugin.cs`): a Gemini-backed
  `ChatCompletionAgent` that writes and executes read-only SQL to gather supporting evidence.
  Guarded by `SqlGuard` (SELECT-only, no chaining, row cap) and a Postgres role with
  `SELECT`-only grants.
- **Agent C — Reporter** (`ReporterAgent.cs`): a second Gemini-backed agent that drafts a
  Markdown root-cause report, instructed to cite only values present in the supplied data.
- **Cost guardrail** (`UsageTracker.cs`): logs token usage and an estimated dollar cost per
  cycle to the console and the report footer — a plain log, not real observability.

## Running it

1. Start Postgres with seeded synthetic data (30 days of hourly production data across 3
   lines, with one injected yield-collapse anomaly on Line 2 in the most recent hours):

   ```bash
   cd db && docker compose up -d
   ```

2. Add your Gemini API key to `src/AeroMindIQ.Console/appsettings.Development.json`
   (gitignored — copy `appsettings.Development.json.example` if it doesn't exist yet):

   ```json
   { "Gemini": { "ApiKey": "YOUR_KEY" } }
   ```

3. Run one detection cycle:

   ```bash
   dotnet run --project src/AeroMindIQ.Console
   ```

   A markdown report is written to `reports/`, and the console prints token usage + estimated
   cost for the cycle.

## Follow-up milestones (not built yet)

1. Replace the Auditor's 3-sigma rule with a trained Isolation Forest / One-Class SVM over
   multiple features (cycle time, temperature variance, item volume).
2. A Reviewer/critic agent that checks the Fetcher's SQL before execution, with a max-retry cap.
3. Langfuse + OpenTelemetry: real tracing across agent handoffs, cost dashboards.
4. An LLM-as-judge groundedness eval over the Reporter's output.

## Testing

```bash
dotnet test                                    # fast, free, offline — 43 tests, ~70ms
dotnet test --filter Category=Live             # opt-in: needs local Postgres + a real API key
```

The default run mocks every LLM call (`Fakes/FakeChatCompletionService.cs`,
`Fakes/FakeMessageService.cs`) — no network, no token cost. This is what caught the real bug
found during Phase 3 verification: the custom Anthropic connector's tool-calling loop
(`AnthropicChatCompletionServiceTests.cs` reproduces it directly). `Category=Live` tests hit
the real Claude API and the local Postgres from `db/docker-compose.yml`; they're excluded by
default since they cost real tokens and need infrastructure a bare checkout won't have.
