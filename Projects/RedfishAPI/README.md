# Redfish API Lab

A runnable demo of the shared [`redfish_toolkit`](../../Shared/redfish_toolkit) Python
library against a mock BMC — no real server hardware needed.

Two pieces:

- **`redfish_emulator/`** — the [DMTF Redfish Interface Emulator](https://github.com/DMTF/Redfish-Interface-Emulator)
  (third-party, DMTF copyright — see its `LICENSE.md`), which serves a standards-compliant
  Redfish service on localhost, backed by static mockup resources.

  > **Vendored snapshot, not a live clone:** this folder is a copy of upstream
  > **v1.2.2** (commit [`5f51d8a`](https://github.com/DMTF/Redfish-Interface-Emulator/commit/5f51d8a),
  > `.git` removed) so the demo is self-contained and runs straight from a clone of this
  > monorepo. It does not track upstream — to take a newer emulator release, re-copy the
  > upstream tree over this folder (excluding `.git/` and `venv/`) and commit the refresh
  > as a single change, noting the new version here.
- **`test_emulator.py`** — a walkthrough script driving the toolkit against the emulator:
  discovers Systems/Chassis/Managers, reads and changes power state, samples BIOS
  attributes, reads thermal/power telemetry, and manages BMC accounts.

## Run it

1. Start the emulator:

   ```bash
   cd redfish_emulator
   pip3 install -r requirements.txt
   python3 emulator.py -port 5001
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
