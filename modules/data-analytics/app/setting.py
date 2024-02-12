import os
from typing import Any, Dict, List, Optional, Union
from pydantic_settings import BaseSettings

from app.utils import get_from_env, str_to_bool

class Settings(BaseSettings):

    PROJECT_NAME: str = "data-analytics-server"

    IS_PRO: bool =  get_from_env("IS_PRO", False, type_cast=str_to_bool)
    TEST: bool = get_from_env("TEST", True, type_cast=str_to_bool)
    SUFFIX: str = os.getenv("SUFFIX", "")

    KAFKA_HOSTS: Optional[str] = os.getenv("KAFKA_HOSTS", "localhost:29092")
    KAFKA_SECURITY_PROTOCOL: Optional[str] = os.getenv("KAFKA_SECURITY_PROTOCOL", None)
    KAFKA_SASL_MECHANISM: Optional[str] = os.getenv("KAFKA_SASL_MECHANISM", None)
    KAFKA_SASL_USER: Optional[str] = os.getenv("KAFKA_SASL_USER", None)
    KAFKA_SASL_PASSWORD: Optional[str] = os.getenv("KAFKA_SASL_PASSWORD", None)
    KAFKA_PRODUCER_RETRIES: int = 3
    KAFKA_PRODUCER_ENABLED: bool = get_from_env("KAFKA_PRODUCER_ENABLED", True, type_cast=str_to_bool)
    KAFKA_PREFIX: str = os.getenv("KAFKA_PREFIX", "")

    CLICKHOUSE_HOST: str = os.getenv("CLICKHOUSE_HOST", "localhost")
    CLICKHOUSE_ALT_HOST: Optional[str] = os.getenv("CLICKHOUSE_ALT_HOST", None)
    CLICKHOUSE_PORT: int = get_from_env("CLICKHOUSE_PORT", 9000, type_cast=int)
    CLICKHOUSE_HTTP_PORT: int = get_from_env("CLICKHOUSE_HTTP_PORT", 8123, type_cast=int)
    CLICKHOUSE_CLUSTER: str = os.getenv("CLICKHOUSE_CLUSTER", "featbit_ch_cluster")
    CLICKHOUSE_DATABASE: str = os.getenv("CLICKHOUSE_DATABASE", "featbit") + SUFFIX
    CLICKHOUSE_SECURE: bool = get_from_env("CLICKHOUSE_SECURE", False, type_cast=str_to_bool)
    CLICKHOUSE_USER: str = os.getenv("CLICKHOUSE_USER", "default")
    CLICKHOUSE_PASSWORD: str = os.getenv("CLICKHOUSE_PASSWORD", "")
    CLICKHOUSE_CA: Optional[str] = os.getenv("CLICKHOUSE_CA", None)
    CLICKHOUSE_VERIFY: bool = get_from_env("CLICKHOUSE_VERIFY", True, type_cast=str_to_bool)
    CLICKHOUSE_CONN_POOL_MIN: int = get_from_env("CLICKHOUSE_CONN_POOL_MIN", 20, type_cast=int)
    CLICKHOUSE_CONN_POOL_MAX: int = get_from_env("CLICKHOUSE_CONN_POOL_MAX", 1000, type_cast=int)
    CLICKHOUSE_ENABLE_STORAGE_POLICY: bool = get_from_env("CLICKHOUSE_ENABLE_STORAGE_POLICY", False, type_cast=str_to_bool)
    CLICKHOUSE_KAFKA_HOSTS: str = os.getenv("CLICKHOUSE_KAFKA_HOSTS", "kafka:9092")
    CLICKHOUSE_REPLICATION: bool = get_from_env("CLICKHOUSE_REPLICATION", True, type_cast=str_to_bool)

    CACHE_TYPE: str = os.getenv("CACHE_TYPE", "RedisCache")
    CACHE_KEY_PREFIX: str = "da-server"
    REDIS_USER: Optional[str] = os.getenv("REDIS_USER", None)
    REDIS_PASSWORD: Optional[str] = os.getenv("REDIS_PASSWORD", None)
    REDIS_DB: int = get_from_env("REDIS_DB", 0, type_cast=int)
    REDIS_SSL: bool = get_from_env("REDIS_SSL", False, type_cast=str_to_bool)

    REDIS_HOST: str = os.getenv("REDIS_HOST", "localhost")
    REDIS_PORT: int = get_from_env("REDIS_PORT", 6379, type_cast=int)

    REDIS_CLUSTER_HOST_PORT_PAIRS: str = os.getenv("REDIS_CLUSTER_HOST_PORT_PAIRS", "localhost:6379")

    REDIS_SENTINEL_HOST_PORT_PAIRS: str = os.getenv("REDIS_SENTINEL_HOST_PORT_PAIRS", "localhost:26379")
    REDIS_SENTINEL_PASSWORD: Optional[str]= os.getenv("REDIS_SENTINEL_PASSWORD", None)
    REDIS_SENTINEL_MASTER_SET: str = os.getenv("REDIS_SENTINEL_MASTER_SET", "mymaster")

    MONGO_URI: str = os.getenv("MONGO_URI", "mongodb://admin:password@localhost:27017")
    MONGO_DB: str = os.getenv("MONGO_INITDB_DATABASE", "featbit")
    MONGO_DB_EVENTS_COLLECTION: str = "Events"

    SHELL_PLUS_PRINT_SQL: bool = True if TEST else False

    DATE_ISO_FMT: str = '%Y-%m-%dT%H:%M:%S.%f'

    DATE_SIM_FMT: str = '%Y-%m-%d %H:%M:%S'
    DATE_UTC_FMT: str = '%Y-%m-%dT%H:%M:%SZ'

#     DEFAULT_LOGGING_CONFIG: str = {
#         'version': 1,
#         'disable_existing_loggers': False,
#         'formatters': {'default': {
#             'format': '%(asctime)s %(levelname)s [%(name)s] [%(filename)s:%(lineno)d] [trace_id=%(otelTraceID)s span_id=%(otelSpanID)s resource.service.name=%(otelServiceName)s trace_sampled=%(otelTraceSampled)s] - %(message)s',
#         }},
#         'handlers': {'wsgi': {
#             'class': 'logging.StreamHandler',
#             'stream': 'ext://flask.logging.wsgi_errors_stream',
#             'formatter': 'default'
#         }},
#         'root': {
#             'level': 'INFO',
#             'handlers': ['wsgi']
#         }
# }

settings = Settings()