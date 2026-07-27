# Architecture

## System layers

```mermaid
flowchart TB
    subgraph Clients["Consumers"]
        C1["Redfish CLI / curl / Postman"]
        C2["Swagger UI"]
        C3["C# xUnit Test Harness"]
    end

    subgraph API["ASP.NET Core Web API — Mock BMC"]
        MW["Middleware Pipeline<br/>(OData annotation, ETag, Auth, Error→ExtendedInfo)"]
        subgraph Controllers["Redfish Controllers"]
            SR["ServiceRoot /redfish/v1"]
            SYS["Systems / Processors / Memory / PCIeDevices"]
            CH["Chassis / Thermal / Power"]
            MGR["Managers (the BMC)"]
            TEL["TelemetryService — MetricReports"]
            TSK["TaskService (async diagnostics)"]
            SESS["SessionService / Auth"]
        end
    end

    subgraph Domain["Domain & Services (RedfishEmulator.Core)"]
        INV["Inventory Service"]
        DIAG["Diagnostic Engine (pass runner + fault injection)"]
        TELS["Telemetry Generator"]
        FAULT["Fault Injection Profiles"]
    end

    subgraph Store["State / Persistence"]
        SEED["Seed Data (JSON) — RedfishEmulator.Data"]
        STATE["In-memory Repository"]
    end

    C1 & C2 & C3 --> MW --> Controllers
    Controllers --> INV & DIAG & TELS
    DIAG --> FAULT
    INV & DIAG & TELS --> STATE
    SEED --> STATE
```

## Redfish resource tree

```mermaid
flowchart LR
    ROOT["/redfish/v1 — ServiceRoot"]
    ROOT --> SYS["/Systems"]
    ROOT --> CHA["/Chassis"]
    ROOT --> MGR["/Managers"]
    ROOT --> TEL["/TelemetryService"]
    ROOT --> TSK["/TaskService"]
    ROOT --> SES["/SessionService"]

    SYS --> S1["/Systems/1"]
    S1 --> PROC["/Processors (CPU + GPU accelerators)"]
    S1 --> MEM["/Memory"]
    S1 --> PCIE["/PCIeDevices"]
    S1 --> ACT["Actions: Reset, Oem.RunDiagnostics"]

    CHA --> TH["/Thermal"]
    CHA --> PW["/Power"]

    TEL --> MR["/MetricReports"]
    TSK --> T1["/Tasks/{id}"]
```

## Design principles

- **`Core` has no ASP.NET dependency.** Redfish models and the diagnostic engine are pure
  domain logic, testable without a web host.
- **Redfish authenticity.** Every resource carries `@odata.id` / `@odata.type` /
  `@odata.context`; collections use `Members` + `Members@odata.count`; health is reported
  via the standard `Status { State, Health, HealthRollup }` object; errors use the Redfish
  `ExtendedInfo` message-registry shape; long-running diagnostics return `202 Accepted`
  with a `TaskMonitor` URL.
- **Three test projects by intent.** Unit (logic) / Contract (Redfish + OData conformance) /
  Stress (concurrency + fault modes). This split is the "validation framework" story.
- **Fault injection as pluggable profiles.** Each failure mode implements `IFaultProfile`,
  so stress tests can activate e.g. "GPU fell off the bus" and assert the engine reports
  `Health: Critical`.
