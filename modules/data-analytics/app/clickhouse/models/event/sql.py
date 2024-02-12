from app.clickhouse.models import cluster
from app.setting import settings


def _internal_event_table_name(is_kafka=False):
    if is_kafka:
        return 'kafka_events_queue'
    return 'events'


def event_table_name():
    if settings.CLICKHOUSE_REPLICATION:
        return 'distributed_events'
    return 'events'


def _event_partition_by():
    return 'PARTITION BY (env_id, toYYYYMM(timestamp))'


def _event_order_by():
    return 'ORDER BY (env_id, toDate(timestamp), event, cityHash64(distinct_id))'


def _event_sample_by():
    return 'SAMPLE BY cityHash64(distinct_id)'

EVENT_MERGE_TREE_EXTRA_SQL = """{partition_by}
{order_by}
{sample_by}
{storage_policy}"""





INSERT_EVENT_SQL = f"""
INSERT INTO {_internal_event_table_name()} (uuid, distinct_id, env_id, event, properties, timestamp, _timestamp, _offset)
VALUES (%(uuid)s, %(distinct_id)s, %(env_id)s, %(event)s, %(properties)s, %(timestamp)s, now(), 0)
"""

BULK_INSERT_EVENT_SQL = f"""
INSERT INTO {_internal_event_table_name()} (uuid, distinct_id, env_id, event, properties, timestamp, _timestamp, _offset)
VALUES
"""


# MERGE_EVENTS_SQL = f"OPTIMIZE TABLE {_internal_event_table_name()} {cluster()} FINAL"
