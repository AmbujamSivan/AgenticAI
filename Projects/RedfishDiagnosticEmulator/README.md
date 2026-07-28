# Redfish-Compliant Platform Diagnostic & Telemetry Emulator

A lightweight, mock **BMC (Baseboard Management Controller)** that exposes
[DMTF Redfish](https://www.dmtf.org/standards/redfish) REST APIs to report component
inventory (CPU, GPU accelerators, PCIe devices), stream platform telemetry, and run
automated diagnostic passes — including under injected, edge-case hardware failure modes.

Built to demonstrate familiarity with cloud-standard hardware-management protocols,
RESTful service architecture, platform bring-up diagnostics, and the test-automation
craft used to validate hardware-management interfaces.

## Tech stack

- **C# / ASP.NET Core** (.NET 9) — Redfish REST service
- **Redfish data model** (DMTF DSP0266) with OData annotations
- **Swagger / OpenAPI** — interactive API explorer
- **xUnit** — unit, contract, and stress/fault-injection test suites

## Solution layout

```
RedfishEmulator.sln
├── src/
│   ├── RedfishEmulator.Api      ASP.NET Core host — the mock BMC (controllers, Swagger)
│   ├── RedfishEmulator.Core     Domain: Redfish resource models, diagnostics, telemetry
│   └── RedfishEmulator.Data     Seed hardware inventory (JSON)
└── tests/
    ├── RedfishEmulator.UnitTests      Pure domain logic (engine, models, generators)
    ├── RedfishEmulator.ContractTests  Redfish/OData conformance against the live host
    └── RedfishEmulator.StressTests    Concurrency + fault-injection load scenarios
```

## Getting started

Requires the .NET 9 SDK (pinned via `global.json`).

```bash
# Build everything
dotnet build

# Run the full test suite
dotnet test

# Run the emulator (Development enables Swagger UI)
dotnet run --project src/RedfishEmulator.Api
```

Then browse:

| URL | What it serves |
|-----|----------------|
| `/redfish` | Redfish protocol version map |
| `/redfish/v1` | ServiceRoot — the entry point to the resource tree |
| `/redfish/v1/Systems/1` | ComputerSystem — inventory, summaries, health rollup |
| `/redfish/v1/TelemetryService/MetricReports` | Live CPU/GPU/power metric reports |
| `/swagger` | Interactive OpenAPI explorer |

### Run a diagnostic pass

```bash
curl -X POST http://localhost:5199/redfish/v1/Systems/1/Actions/Oem/RedfishEmulator.RunDiagnostics
# → 202 Accepted, Location: /redfish/v1/TaskService/Tasks/1  (poll it for pass/fail results)
```

### Inject a failure mode (OEM)

```bash
# Make a GPU "fall off the bus", then re-run diagnostics to see it fail
curl -X POST http://localhost:5199/redfish/v1/Oem/RedfishEmulator/FaultInjection/GpuOffBus/Activate
# Available profiles: GpuOffBus, PcieLinkDown, MemoryEcc, ThermalTrip
# Clear one, or reset the whole platform:
curl -X POST http://localhost:5199/redfish/v1/Oem/RedfishEmulator/FaultInjection/Reset
```

## Build phases

| Phase | Focus | Status |
|-------|-------|--------|
| **0** | Solution scaffold, ServiceRoot, Swagger, test harness | ✅ Done |
| **1** | Redfish core model + OData annotation infrastructure (collections, `$metadata`, service doc) | ✅ Done |
| **2** | Inventory read APIs (Systems, Processors incl. GPU accelerators, Memory, PCIe, Chassis) | ✅ Done |
| **3** | TelemetryService + MetricReports + Chassis Thermal/Power (time-varying sensors) | ✅ Done |
| **4** | Diagnostics engine — RunDiagnostics action, async TaskService, per-component passes | ✅ Done |
| **5** | Fault-injection profiles (GPU off bus, PCIe link down, ECC, thermal trip) + OEM toggle | ✅ Done |
| 6 | Auth (SessionService), ETag, Redfish error responses | ⬜ |
| 7 | Full test automation (contract, schema, stress, fault modes) | ⬜ |
| 8 | Docs, OpenAPI export, Docker | ⬜ |

See [`docs/architecture.md`](docs/architecture.md) for the architecture and resource tree.
