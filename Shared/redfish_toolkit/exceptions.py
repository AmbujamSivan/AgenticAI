class RedfishError(Exception):
    pass


class RedfishAuthError(RedfishError):
    pass


class RedfishRequestError(RedfishError):
    pass
