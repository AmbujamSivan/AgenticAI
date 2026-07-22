from .client import RedfishClient

VALID_RESET_TYPES = {
    "On",
    "ForceOff",
    "GracefulShutdown",
    "GracefulRestart",
    "ForceRestart",
    "Nmi",
    "PushPowerButton",
    "PowerCycle",
}


class Systems:
    """Power/reset, BIOS settings, and error logs (SEL equivalent) for one ComputerSystem."""

    def __init__(self, client: RedfishClient, system_path: str):
        self.client = client
        self.path = system_path

    def get_summary(self) -> dict:
        return self.client.get(self.path)

    def get_power_state(self) -> str:
        return self.get_summary()["PowerState"]

    def reset(self, reset_type: str) -> None:
        if reset_type not in VALID_RESET_TYPES:
            raise ValueError(f"Unsupported ResetType '{reset_type}', expected one of {VALID_RESET_TYPES}")
        # Action target is read from the resource rather than hardcoded, since it
        # varies by vendor (e.g. iDRAC vs iLO put it under different paths).
        target = self.get_summary()["Actions"]["#ComputerSystem.Reset"]["target"]
        self.client.post(target, {"ResetType": reset_type})

    def power_on(self) -> None:
        self.reset("On")

    def force_off(self) -> None:
        self.reset("ForceOff")

    def power_cycle(self) -> None:
        self.reset("PowerCycle")

    def get_bios_attributes(self) -> dict:
        bios_path = self.get_summary()["Bios"]["@odata.id"]
        return self.client.get(bios_path).get("Attributes", {})

    def set_bios_attributes(self, attributes: dict) -> dict:
        # Writes go to the pending Settings resource; most vendors only apply
        # them on the next reboot, not immediately.
        bios_path = self.get_summary()["Bios"]["@odata.id"]
        return self.client.patch(f"{bios_path}/Settings", {"Attributes": attributes})

    def list_log_services(self) -> list:
        log_services = self.get_summary().get("LogServices")
        if not log_services:
            return []
        return self.client.get_collection_members(log_services["@odata.id"])

    def get_log_entries(self, log_service_path: str) -> list:
        entries_path = self.client.get(log_service_path)["Entries"]["@odata.id"]
        return self.client.get(entries_path).get("Members", [])

    def clear_log(self, log_service_path: str) -> None:
        target = self.client.get(log_service_path)["Actions"]["#LogService.ClearLog"]["target"]
        self.client.post(target)
