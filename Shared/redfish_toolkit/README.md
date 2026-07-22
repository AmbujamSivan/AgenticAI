# redfish_toolkit

A lightweight Python client library for the DMTF **Redfish** out-of-band management API
(the standard BMC interface on modern servers). Shared across projects in this monorepo.

## Modules

| Module | Purpose |
|---|---|
| `client.py` | `RedfishClient` — session auth + JSON get/patch/post/delete against one BMC; collection discovery (`discover_systems/chassis/managers`) |
| `systems.py` | `Systems` — power state & reset actions, BIOS attributes, log services (list/read/clear) |
| `chassis.py` | `Chassis` — fans, temperatures, power supplies, power consumption, inventory |
| `managers.py` | `Manager` — BMC summary, reset, network protocol get/set |
| `accounts.py` | `Accounts` — list/create/delete BMC user accounts |
| `exceptions.py` | `RedfishAuthError`, `RedfishRequestError` |

## Usage

```python
from redfish_toolkit import RedfishClient
from redfish_toolkit.systems import Systems

with RedfishClient("bmc-host", "admin", "password") as client:
    system = Systems(client, client.discover_systems()[0])
    print(system.get_power_state())
    print(system.get_bios_attributes())
```

Only dependency: `requests`.

See `example.py` for a fuller walkthrough, and `Projects/RedfishAPI` for a runnable
demo against the DMTF Redfish Interface Emulator (no real hardware needed).
