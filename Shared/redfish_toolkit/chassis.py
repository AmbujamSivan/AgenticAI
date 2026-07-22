from typing import Optional

from .client import RedfishClient


class Chassis:
    """Fan/temperature sensors, PSU/power draw, and physical inventory for one chassis."""

    def __init__(self, client: RedfishClient, chassis_path: str):
        self.client = client
        self.path = chassis_path

    def get_summary(self) -> dict:
        return self.client.get(self.path)

    def get_fans(self) -> list:
        return self.client.get(f"{self.path}/Thermal").get("Fans", [])

    def get_temperatures(self) -> list:
        return self.client.get(f"{self.path}/Thermal").get("Temperatures", [])

    def get_power_supplies(self) -> list:
        return self.client.get(f"{self.path}/Power").get("PowerSupplies", [])

    def get_power_consumption_watts(self) -> Optional[float]:
        controls = self.client.get(f"{self.path}/Power").get("PowerControl", [])
        return controls[0].get("PowerConsumedWatts") if controls else None

    def get_inventory(self) -> dict:
        summary = self.get_summary()
        inventory = {
            "Manufacturer": summary.get("Manufacturer"),
            "Model": summary.get("Model"),
            "SerialNumber": summary.get("SerialNumber"),
            "PartNumber": summary.get("PartNumber"),
            "AssetTag": summary.get("AssetTag"),
        }
        assembly = summary.get("Assembly", {}).get("@odata.id")
        if assembly:
            inventory["Assemblies"] = self.client.get(assembly).get("Assemblies", [])
        return inventory
