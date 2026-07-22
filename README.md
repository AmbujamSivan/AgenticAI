# Agentic AI Portfolio 🤖🚀

Welcome to my Agentic AI laboratory. This repository is a monorepo of autonomous,
multi-agent systems. Each project is self-contained and showcases a different
architectural pattern of modern AI engineering — multi-agent collaboration, agentic
tool-use, LLM-as-judge grounding, and local-model orchestration.

---

## 🛠️ Tech Stack & Concepts
* **Languages:** C# (.NET 9/10), Python
* **Frameworks/Orchestration:** Semantic Kernel, OpenTelemetry + Langfuse, FastAPI (ML sidecar)
* **LLM Providers:** Config-switchable — Ollama (local), OpenAI, Azure OpenAI, Gemini, Claude, OpenAI-compatible endpoints
* **Core Concepts:** Multi-agent collaboration, function calling, agent self-critique, groundedness judging, deterministic fallbacks, cost guardrails

---

## 📂 Featured Projects

### 🏭 1. AeroMind IQ — Manufacturing Anomaly Investigation Squad
A multi-agent backend that watches a live manufacturing database for production anomalies
and coordinates five Semantic Kernel agents — Auditor, Reviewer, Fetcher, Reporter, and a
Groundedness Judge — to investigate, draft a root-cause report, and grade the report
against the actual query evidence.
* **Architecture:** Supervisor cycle with an in-line critic (SQL review before execution) and an LLM-as-judge grading pass.
* **Core Tech:** C# (.NET 10), Semantic Kernel, Postgres, scikit-learn Isolation Forest (FastAPI sidecar), OpenTelemetry → Langfuse, per-cycle token/cost tracking.
* **[Explore Project 📂](./Projects/AeroMindIQ)**

### 🔎 2. RCA Engine — Agentic Failure Triage for Server Hardware
An autonomous agent that triages server-node hardware failures from a raw diagnostic
bundle (Redfish BMC events, PCIe AER registers, kernel dmesg), isolates the failing
subsystem via LLM tool-calling against native C# parsers, and emits a structured RCA
report (JSON + Markdown).
* **Architecture:** Two-phase agent loop (investigate with tools → validated structured submission) with a deterministic rule-based fallback so the pipeline always ships a report.
* **Core Tech:** C# (.NET 9), Semantic Kernel, local quantized LLM via Ollama (llama3.2), xUnit, Docker Compose.
* **[Explore Project 📂](./Projects/RCA_Engine)** | **[Interactive Demo 🖥️](./Projects/RCA_Engine/docs/index.html)**

### 🖧 3. Redfish API Lab — BMC Client Toolkit + Mock Service
A hands-on lab for the server management plane: a lightweight Python Redfish client
library (shared across the monorepo) driven against the DMTF Redfish Interface Emulator,
covering power actions, BIOS attributes, thermal/power telemetry, event logs, and BMC
account management — no real hardware needed.
* **Architecture:** Reusable client library in `Shared/`, spec-faithful mock BMC as the demo target.
* **Core Tech:** Python, DMTF Redfish schema, requests-based session auth.
* **[Explore Project 📂](./Projects/RedfishAPI)** | **[Library 📚](./Shared/redfish_toolkit)**

---

## 🧰 Shared Libraries
* **[`Shared/redfish_toolkit`](./Shared/redfish_toolkit)** — Python Redfish/BMC client (systems, chassis, managers, accounts) used by the Redfish API Lab and available to any project needing out-of-band hardware telemetry.

---

## ⚙️ How to Run
> **Note:** this repo uses a git **submodule** (the DMTF Redfish emulator in the Redfish
> API Lab). Clone with `git clone --recurse-submodules`, or run
> `git submodule update --init` after a plain clone — otherwise that folder will be empty.

Each project directory has its own README with setup instructions. Both projects use
Docker Compose for their infrastructure (Postgres/Langfuse for AeroMind IQ; Ollama for
RCA Engine), and both degrade gracefully when optional services are unavailable.
