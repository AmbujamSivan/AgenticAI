# AeroMind IQ

A multi-agent backend that watches a live manufacturing database for production
anomalies and coordinates a squad of Semantic Kernel agents to investigate, draft a
root-cause report, and grade their own report's groundedness.

The LLM provider is a config switch, not a hard dependency on one vendor — Gemini,
Claude, OpenAI, and any OpenAI-compatible endpoint (DeepSeek, Groq, etc.) are all
supported via `appsettings.json`.

## Architecture

- **Agent A — Auditor** (`AuditorAgent.cs`): watches `production_runs` for anomalies.
  Primary path scores recent rows through a scikit-learn **Isolation Forest** (`ml/`,
  served by a small FastAPI process) over multiple features — cycle time, temperature
  variance, units produced, yield — so combinations that don't trip any single-column
  threshold still get caught. Falls back automatically to a deterministic 3-sigma yield
  check if the scoring service is unreachable.
- **Agent B — Reviewer** (`ReviewerAgent.cs`): a critic that checks the Fetcher's proposed
  SQL before it runs — logic errors (wrong table/column, a WHERE clause that can't relate
  to the anomaly) and context leaks (unbounded `SELECT *`). Sits inside `DatabasePlugin`,
  with a bounded retry cap so a chain of rejections can't stall the investigation forever.
- **Agent C — Fetcher** (`FetcherAgent.cs` + `DatabasePlugin.cs`): writes and executes
  read-only SQL (reviewed by the Reviewer) to gather supporting evidence. Guarded by
  `SqlGuard` (SELECT-only, no statement chaining, row cap) and a Postgres role with
  `SELECT`-only grants — defense in depth beyond the prompt-level instructions.
- **Agent D — Reporter** (`ReporterAgent.cs`): drafts a Markdown root-cause report,
  instructed to cite only values present in the supplied data.
- **Agent E — Judge** (`GroundednessJudge.cs`): grades the Reporter's output against the
  Fetcher's actual query results, flagging any claim not supported by the data. Runs
  synchronously at the end of each cycle; not yet calibrated against a labeled eval set,
  which the report footer says explicitly.
- **LLM provider layer** (`LlmProviderConfig.cs` + `LlmKernelFactory.cs`): the one place
  provider-branching logic lives. `AnthropicChatCompletionService.cs` is a custom
  `IChatCompletionService` wrapping Anthropic's official .NET SDK — no official Semantic
  Kernel connector exists for Anthropic's direct API outside Amazon Bedrock, so this
  implements the tool-calling auto-invoke loop itself (SK's `ChatCompletionAgent` doesn't
  drive one for you). Gemini/OpenAI/OpenAI-compatible providers reuse SK's official
  connectors.
- **Observability** (`Telemetry.cs` + `observability/`): OpenTelemetry traces exported to
  a self-hosted Langfuse stack — one root span per cycle (`aeromindiq.cycle`) with every
  agent/tool call nested underneath.
- **Cost guardrail** (`UsageTracker.cs`): logs token usage and an estimated dollar cost
  per cycle (per-model pricing table) to the console and the report footer.

## Prerequisites

This is a polyglot project — running the full pipeline needs:

- **.NET 10 SDK**
- **Docker** (Postgres + the self-hosted Langfuse stack)
- **Python 3** (for the Isolation Forest training script and scoring service, `ml/`)
- An API key for at least one LLM provider (Gemini, Claude, OpenAI, or a DeepSeek-style
  OpenAI-compatible endpoint)

## Running it

1. Start Postgres with seeded synthetic data (30 days of hourly production data across 3
   lines, with one injected multi-dimensional anomaly — a yield/cycle-time/temperature
   spike together on Line 2 — in the most recent hours):

   ```bash
   cd db && docker compose up -d
   ```

2. Train and serve the Isolation Forest scoring service:

   ```bash
   cd ml
   python3 -m venv .venv && source .venv/bin/activate
   pip install -r requirements.txt
   python train.py                 # trains on rows older than the last 4 hours
   uvicorn service:app --port 8500
   ```

   If this service is down when the app runs, the Auditor logs a warning and falls back
   to the 3-sigma check automatically — it doesn't hard-fail the cycle.

3. (Optional) Bring up self-hosted Langfuse for tracing:

   ```bash
   cd observability && docker compose up -d
   ```

   Open `http://localhost:3000`, create a project, and put the public/secret keys into
   `appsettings.Development.json` under `Langfuse`. Without this, the app still runs —
   `Telemetry.Build` just logs that tracing is disabled and skips it.

4. Pick an LLM provider and add its key to
   `src/AeroMindIQ.Console/appsettings.Development.json` (gitignored — copy
   `appsettings.Development.json.example` if it doesn't exist yet):

   ```json
   {
     "LlmProvider": "Claude",
     "Providers": {
       "Claude": { "ApiKey": "YOUR_KEY" }
     }
   }
   ```

   `LlmProvider` selects which entry under `Providers` is active — swap it to `Gemini`,
   `OpenAI`, or `OpenAICompatible` (with a `BaseUrl`, e.g. DeepSeek) without touching code.

5. Run one detection cycle:

   ```bash
   dotnet run --project src/AeroMindIQ.Console
   ```

   A markdown report is written to `reports/`, with a groundedness verdict and token
   usage/estimated cost appended to the footer. If Langfuse is running, a trace for the
   cycle appears in its UI.

## Testing

```bash
dotnet test                                    # fast, free, offline — 43 tests, ~70ms
dotnet test --filter Category=Live             # opt-in: needs local Postgres + a real API key
```

The default run mocks every LLM call (`Fakes/FakeChatCompletionService.cs`,
`Fakes/FakeMessageService.cs`) — no network, no token cost. This is what caught a real bug
found during live verification: the custom Anthropic connector's tool-calling loop
(`AnthropicChatCompletionServiceTests.cs` reproduces it directly). `Category=Live` tests hit
the real Claude API and the local Postgres from `db/docker-compose.yml`; they're excluded by
default since they cost real tokens and need infrastructure a bare checkout won't have.

## Not built yet

- A recruiter-facing demo page/web UI for bringing your own API key — the provider-switch
  config shape (`Providers` dict) is designed for this, but no UI exists yet.
- A labeled calibration eval set for the groundedness Judge.
- Per-call OpenTelemetry spans for the Anthropic connector specifically (tool-call spans
  show up in Langfuse today; the raw chat-completion calls themselves don't yet, unlike
  the official Gemini/OpenAI connectors).
