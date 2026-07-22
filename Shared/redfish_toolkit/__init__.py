from .accounts import Accounts
from .chassis import Chassis
from .client import RedfishClient
from .exceptions import RedfishAuthError, RedfishError, RedfishRequestError
from .managers import Manager
from .systems import Systems

__all__ = [
    "RedfishClient",
    "RedfishServer",
    "Systems",
    "Chassis",
    "Manager",
    "Accounts",
    "RedfishError",
    "RedfishAuthError",
    "RedfishRequestError",
]


class RedfishServer:
    """Convenience wrapper bundling Systems/Chassis/Manager/Accounts for one BMC endpoint.

    For a rack, instantiate one RedfishServer per host/IP.
    """

    def __init__(self, host: str, username: str, password: str, verify_ssl: bool = False):
        self.client = RedfishClient(host, username, password, verify_ssl)
        self.client.login()
        self.systems = Systems(self.client, self.client.discover_systems()[0])
        self.chassis = Chassis(self.client, self.client.discover_chassis()[0])
        self.manager = Manager(self.client, self.client.discover_managers()[0])
        self.accounts = Accounts(self.client)

    def close(self) -> None:
        self.client.logout()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()
