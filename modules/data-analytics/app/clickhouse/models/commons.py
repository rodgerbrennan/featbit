from app.setting import settings


def cluster() -> str:
    if settings.CLICKHOUSE_REPLICATION:
        return f"ON CLUSTER {settings.CLICKHOUSE_CLUSTER}"
    else:
        return ""


def storage_policy() -> str:
    return "settings storage_policy = 'hot_to_cold'" if settings.CLICKHOUSE_ENABLE_STORAGE_POLICY else ""


def optimize_tables() -> None:
    from app.clickhouse.models.event.util import optimize_events
    optimize_events()
