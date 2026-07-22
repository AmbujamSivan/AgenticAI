"""Usage example: query/control a rack of servers via Redfish.

Set REDFISH_HOSTS to a comma-separated list of BMC IPs/hostnames, and
REDFISH_USER / REDFISH_PASS for credentials, before running.
"""

import os

from redfish_toolkit import RedfishServer

HOSTS = os.environ.get("REDFISH_HOSTS", "").split(",")
USERNAME = os.environ["REDFISH_USER"]
PASSWORD = os.environ["REDFISH_PASS"]


def audit_server(host: str) -> None:
    with RedfishServer(host, USERNAME, PASSWORD) as server:
        print(f"--- {host} ---")

        # Systems: power state, BIOS, error log
        print("Power state:", server.systems.get_power_state())
        print("BIOS attrs (sample):", dict(list(server.systems.get_bios_attributes().items())[:3]))
        log_services = server.systems.list_log_services()
        if log_services:
            print("Recent log entries:", server.systems.get_log_entries(log_services[0])[:3])

        # Chassis: fans, temps, inventory
        print("Fans:", [(f.get("Name"), f.get("Reading")) for f in server.chassis.get_fans()])
        print("Temps:", [(t.get("Name"), t.get("ReadingCelsius")) for t in server.chassis.get_temperatures()])
        print("Inventory:", server.chassis.get_inventory())

        # Managers: BMC info + protocol status
        print("BMC firmware:", server.manager.get_summary().get("FirmwareVersion"))

        # Accounts: current BMC users
        print("Accounts:", [a.get("UserName") for a in server.accounts.list_accounts()])


def force_power_on(host: str) -> None:
    with RedfishServer(host, USERNAME, PASSWORD) as server:
        server.systems.power_on()


if __name__ == "__main__":
    for host in HOSTS:
        audit_server(host.strip())
