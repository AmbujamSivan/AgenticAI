from redfish_toolkit import RedfishClient
from redfish_toolkit.systems import Systems
from redfish_toolkit.chassis import Chassis
from redfish_toolkit.managers import Manager
from redfish_toolkit.accounts import Accounts

client = RedfishClient("localhost:5001", "admin", "password", verify_ssl=True, scheme="http")

systems = client.discover_systems()
chassis = client.discover_chassis()
managers = client.discover_managers()
print("Systems:", systems)
print("Chassis:", chassis)
print("Managers:", managers)

s = Systems(client, systems[0])
print("Power state:", s.get_power_state())
s.power_on()
print("BIOS attrs sample:", dict(list(s.get_bios_attributes().items())[:3]))

c = Chassis(client, chassis[0])
print("Fans:", [(f.get("FanName"), f.get("ReadingRPM")) for f in c.get_fans()])
print("Temps:", [(t.get("Name"), t.get("ReadingCelsius")) for t in c.get_temperatures()])
print("Inventory:", c.get_inventory())

m = Manager(client, managers[0])
print("Manager FW:", m.get_summary().get("FirmwareVersion"))

a = Accounts(client)
print("Accounts:", [acc.get("UserName") for acc in a.list_accounts()])
