# Agentic Root-Cause Analysis (RCA) & Failure Triage Engine

An autonomous **C# / .NET 9** agent that triages server-node hardware failures from a raw
diagnostic bundle. A **Semantic Kernel** orchestrator lets an LLM (local quantized model via
**Ollama**, or OpenAI / Azure OpenAI) plan tool calls against native diagnostic parsers over
**five telemetry sources** — Redfish BMC event logs, PCIe AER error registers, the kernel
log, the platform's **pre-OS boot-progress log** (POST codes / UEFI stages), and the
**DPU's own control-plane console** (SmartNIC-internal telemetry, not just what the host
saw) — isolate the failing subsystem, and emit a **structured RCA report** (JSON + Markdown).

**Demo:** open [`docs/index.html`](docs/index.html) in a browser for an interactive replay of
the agent's investigations across all three sample failure scenarios.

## Architecture

```
                  ┌─────────────────────────────────────────┐
                  │          Raw Diagnostic Bundle          │
                  │  (Redfish Event Logs, PCIe Regs, dmesg) │
                  └────────────────────┬────────────────────┘
                                       │
                                       ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                           .NET 9 RCA Core Engine                         │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │                 Agent Orchestrator (Semantic Kernel)               │  │
│  └───────┬───────────────────────────▲────────────────────────┬───────┘  │
│          │                           │                        │          │
│          │ 1. Prompt + Context       │ 2. Tool Calls          │ 3. Exec  │
│          ▼                           │                        ▼          │
│  ┌───────────────┐           ┌───────┴────────┐       ┌───────────────┐  │
│  │ Local/Cloud   │           │ Function       │       │ Diagnostic    │  │
│  │ LLM Endpoint  │           │ Router         │       │ Native Tools  │  │
│  │ (Ollama/OAI/  │           └────────────────┘       │ (Redfish,     │  │
│  │  Azure OAI)   │                                    │  PCIe, dmesg) │  │
│  └───────────────┘                                    └───────┬───────┘  │
└───────────────────────────────────────────────────────────────┼──────────┘
                                                                │
                                                                ▼
                                                ┌───────────────────────────┐
                                                │   Structured RCA Output   │
                                                │ (JSON / Markdown Report)  │
                                                └───────────────────────────┘
```

### How the agent works

1. **Phase 1 — Investigation.** The orchestrator gives the LLM only the diagnostic tools:
   - `redfish.get_redfish_event_summary` / `get_redfish_events` — BMC system event log
   - `pcie.list_pcie_devices` / `decode_pcie_aer_registers` — decodes AER status registers
     into named error bits per the PCIe spec, flags downtrained links
   - `dmesg.get_dmesg_summary` / `search_dmesg` — kernel log classified into fault classes
     (EDAC corrected/uncorrected errors, MCE, NVMe errors, AER, enumeration, offload, I/O, thermal)
   - `boot.get_boot_progress` — pre-OS platform boot flow: POST codes across SEC/PEI/DXE/BDS,
     stage errors (option-ROM failures, config-read errors), OS-handoff status
   - `dpu.get_dpu_console_summary` / `search_dpu_console` — the DPU's ARM-side control-plane
     log (ATF/UEFI boot chain, NIC firmware, DOCA flow engine) — the device's internal view,
     used to corroborate or refute host-side suspicions
2. **Phase 2 — Verdict.** The `report.submit_rca_report` tool is then exposed and the model
   must submit a structured verdict (category, failing component, root cause, evidence,
   actions, confidence). Incomplete or invalid submissions are **rejected by the tool**,
   forcing the model to retry — this keeps small quantized models honest.
3. **Cross-check.** After the agent submits, the rule-based `DeterministicTriage` scorer runs
   the same evidence independently. If both agree on the category but name different
   components, the agent is challenged to reconcile; if it can't, the evidence-derived
   component wins and the correction is recorded in the report as `[cross-check]` evidence.
4. **Fallback.** The same deterministic scorer runs standalone when no LLM is reachable
   (`--no-llm`) or the agent fails to submit, so the pipeline always produces a report.

## Prerequisites

- .NET 9 SDK
- [Ollama](https://ollama.com) with a tool-calling model: `ollama pull llama3.2`
  (or configure OpenAI / Azure OpenAI in `src/RcaEngine/appsettings.json`)

## Run

```bash
# Agentic triage with the local model (default: Ollama + llama3.2)
dotnet run --project src/RcaEngine -- samples/bundle-memory-ce-storm

# Deterministic-only mode (no LLM needed)
dotnet run --project src/RcaEngine -- samples/bundle-nvme-controller-failure --no-llm

# Override provider/model
dotnet run --project src/RcaEngine -- samples/bundle-pcie-link-degrade --provider Ollama --model llama3.2
```

Reports land in `<bundle-dir>/rca_output/report.{json,md}` (override with `--output`).

Configuration can also come from environment variables with the `RCA_` prefix, e.g.
`RCA_Llm__Ollama__Endpoint=http://ollama:11434`.

## Sample scenarios

| Bundle | Fault signature | Expected verdict |
|---|---|---|
| `bundle-memory-ce-storm` | Sustained EDAC corrected-error storm + BMC ECC-rate-exceeded event, clean PCIe | `MemorySubsystem` — degrading DIMM (predictive failure) |
| `bundle-nvme-controller-failure` | NVMe I/O timeouts → controller-down (`CSTS=0xffffffff`) → device removal, drive health events, AER CompletionTimeout | `StorageNvme` — dead drive controller, not the fabric |
| `bundle-pcie-link-degrade` | Correctable AER storm (RxErr/BadTLP/BadDLLP), link downtrained 16GT/s → 2.5GT/s | `PcieLink` — physical-layer / connector fault, not the endpoint |
| `bundle-dpu-enum-failure` | DPU functions missing from config space (`vendor id 0xffffffff`), BAR assignment failure, probe abort -16; fault visible pre-OS at DXE (config-read + OpROM warnings); DPU console shows the internal root cause: NIC-subsystem firmware image CRC mismatch, watchdog recovery exhausted, host PF config space gated | `PcieEnumeration` — corrupted DPU firmware image; not the host fabric |
| `bundle-dpu-offload-fallback` | `SET_FLOW_TABLE_ENTRY` bad-resource-state errors, OVS falling back to host datapath, link healthy; DPU console confirms steering table at 100% (2M STE entries) with aged-flow reclaim not keeping up | `DpuOffload` — offload engine exhausted; host CPU pressure is the symptom |

## Tests

```bash
dotnet test
```

39 tests cover the AER bit decoder, dmesg classifier, Redfish parser, boot-progress and
DPU-console parsers, and end-to-end deterministic triage of all five sample bundles
(including DPU-internal and boot-stage evidence assertions).

## Docker

```bash
docker compose up --build   # starts Ollama, pulls llama3.2, runs the engine against a bundle
```

## Project layout

```
src/RcaEngine/
  Agents/RcaOrchestrator.cs     # two-phase agent loop + fallback wiring
  Llm/KernelFactory.cs          # Ollama (OpenAI-compat) / OpenAI / Azure OpenAI
  Tools/                        # SK plugins: Redfish, PCIe, dmesg, report submission
  Parsing/                      # testable native parsers (AER bit decode, dmesg classify)
  Triage/DeterministicTriage.cs # rule-based scorer (offline mode / fallback)
  Reporting/                    # Markdown renderer
samples/                        # three synthetic diagnostic bundles
tests/RcaEngine.Tests/          # xUnit suite
```
