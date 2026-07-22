# Redfish API Lab

A runnable demo of the shared [`redfish_toolkit`](../../Shared/redfish_toolkit) Python
library against a mock BMC — no real server hardware needed.

Two pieces:

- **`redfish_emulator/`** — the [DMTF Redfish Interface Emulator](https://github.com/DMTF/Redfish-Interface-Emulator)
  (third-party, DMTF copyright — see its `LICENSE.md`), which serves a standards-compliant
  Redfish service on localhost, backed by static mockup resources.

  > **⚠️ Git submodule — clone recursively.** `redfish_emulator/` is a git **submodule**
  > pinned to upstream **v1.2.2** (commit [`5f51d8a`](https://github.com/DMTF/Redfish-Interface-Emulator/commit/5f51d8a),
  > which includes Reset support, virtual media, and RAID volume management). A plain
  > `git clone` of this monorepo leaves the folder **empty** — get the contents with:
  >
  > ```bash
  > git clone --recurse-submodules https://github.com/AmbujamSivan/AgenticAI.git
  > # or, in an existing clone:
  > git submodule update --init
  > ```
  >
  > To move the pin to a newer upstream commit: `cd redfish_emulator && git pull`, then
  > commit the updated submodule pointer from the monorepo root.
- **`test_emulator.py`** — a walkthrough script driving the toolkit against the emulator:
  discovers Systems/Chassis/Managers, reads and changes power state, samples BIOS
  attributes, reads thermal/power telemetry, and manages BMC accounts.

## Run it

1. Start the emulator (venv lives in this directory, not inside the submodule, so the
   submodule checkout stays clean):

   ```bash
   python3 -m venv venv
   ./venv/bin/pip install -r redfish_emulator/requirements.txt
   cd redfish_emulator && ../venv/bin/python emulator.py -port 5001
   ```

2. In another terminal, run the toolkit demo from this directory (it imports
   `redfish_toolkit` from `Shared/`):

   ```bash
   PYTHONPATH=../../Shared python3 test_emulator.py
   ```

The script targets `localhost:5001` with the emulator's default `admin`/`password`
credentials over plain HTTP — mock-only settings, not for real BMCs.

## Why it's here

Redfish is the management-plane data source for hardware telemetry in this repo —
the [RCA Engine](../RCA_Engine) consumes Redfish event logs when triaging node
failures. This lab provides the client library and a spec-faithful mock service for
developing against that interface.
