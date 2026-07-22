from .client import RedfishClient


class Accounts:
    """BMC user account management (session auth itself lives in RedfishClient.login/logout)."""

    def __init__(self, client: RedfishClient):
        self.client = client

    def list_accounts(self) -> list:
        paths = self.client.get_collection_members("/redfish/v1/AccountService/Accounts")
        return [self.client.get(p) for p in paths]

    def create_account(self, username: str, password: str, role_id: str = "Operator") -> dict:
        return self.client.post(
            "/redfish/v1/AccountService/Accounts",
            {"UserName": username, "Password": password, "RoleId": role_id, "Enabled": True},
        )

    def delete_account(self, account_path: str) -> None:
        self.client.delete(account_path)
