from typing import Optional

import requests
import urllib3

from .exceptions import RedfishAuthError, RedfishRequestError


class RedfishClient:
    """Thin transport layer: session auth + JSON get/patch/post/delete against one BMC."""

    def __init__(self, host: str, username: str, password: str, verify_ssl: bool = False, timeout: int = 10, scheme: str = "https"):
        self.base_url = f"{scheme}://{host}"
        self.username = username
        self.password = password
        self.timeout = timeout
        self.session = requests.Session()
        self.session.verify = verify_ssl
        if not verify_ssl:
            # Most BMCs ship self-signed certs; suppress the noisy per-request warning.
            urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
        self._session_location: Optional[str] = None

    def __enter__(self):
        self.login()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.logout()

    def login(self) -> None:
        resp = self.session.post(
            f"{self.base_url}/redfish/v1/SessionService/Sessions",
            json={"UserName": self.username, "Password": self.password},
            timeout=self.timeout,
        )
        if resp.status_code != 201:
            raise RedfishAuthError(f"Login failed: {resp.status_code} {resp.text}")
        self._session_location = resp.headers.get("Location")
        self.session.headers.update({"X-Auth-Token": resp.headers.get("X-Auth-Token")})

    def logout(self) -> None:
        if self._session_location:
            self.session.delete(f"{self.base_url}{self._session_location}", timeout=self.timeout)
            self._session_location = None
            self.session.headers.pop("X-Auth-Token", None)

    def get(self, path: str) -> dict:
        resp = self.session.get(f"{self.base_url}{path}", timeout=self.timeout)
        self._raise_for_status(resp)
        return resp.json()

    def patch(self, path: str, body: dict) -> dict:
        resp = self.session.patch(f"{self.base_url}{path}", json=body, timeout=self.timeout)
        self._raise_for_status(resp)
        return resp.json() if resp.content else {}

    def post(self, path: str, body: Optional[dict] = None) -> dict:
        resp = self.session.post(f"{self.base_url}{path}", json=body or {}, timeout=self.timeout)
        self._raise_for_status(resp)
        return resp.json() if resp.content else {}

    def delete(self, path: str) -> None:
        resp = self.session.delete(f"{self.base_url}{path}", timeout=self.timeout)
        self._raise_for_status(resp)

    def get_collection_members(self, path: str) -> list:
        """Returns @odata.id strings from a collection resource.

        Handles both the modern top-level 'Members' array (Redfish 1.0+) and
        the older 'Links.Members' nesting still seen on some early BMC firmware.
        """
        collection = self.get(path)
        members = collection.get("Members") or collection.get("Links", {}).get("Members", [])
        return [m["@odata.id"] for m in members]

    def discover_systems(self) -> list:
        return self.get_collection_members("/redfish/v1/Systems")

    def discover_chassis(self) -> list:
        return self.get_collection_members("/redfish/v1/Chassis")

    def discover_managers(self) -> list:
        return self.get_collection_members("/redfish/v1/Managers")

    def _raise_for_status(self, resp: requests.Response) -> None:
        if not resp.ok:
            raise RedfishRequestError(f"{resp.request.method} {resp.url} -> {resp.status_code}: {resp.text}")
