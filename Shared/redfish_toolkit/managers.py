from .client import RedfishClient


class Manager:
    """BMC-level control: reboot the BMC itself and toggle management protocols (IPMI-over-LAN, SSH, etc.)."""

    def __init__(self, client: RedfishClient, manager_path: str):
        self.client = client
        self.path = manager_path

    def get_summary(self) -> dict:
        return self.client.get(self.path)

    def reset(self, reset_type: str = "GracefulRestart") -> None:
        target = self.get_summary()["Actions"]["#Manager.Reset"]["target"]
        self.client.post(target, {"ResetType": reset_type})

    def get_network_protocol(self) -> dict:
        np_path = self.get_summary()["NetworkProtocol"]["@odata.id"]
        return self.client.get(np_path)

    def set_protocol_enabled(self, protocol: str, enabled: bool) -> dict:
        """protocol e.g. 'IPMI', 'SSH', 'SNMP', 'HTTPS' — must match a key in NetworkProtocol."""
        np_path = self.get_summary()["NetworkProtocol"]["@odata.id"]
        return self.client.patch(np_path, {protocol: {"ProtocolEnabled": enabled}})
