import os
import click
import logging
from typing import Any, Callable, Optional, Union
from app.utils import get_from_env, str_to_bool
from app.clickhouse.commands import migrate as migrate_ch
from app.mongodb.commands import migrate as migrate_mongo

logger = logging.getLogger(__name__)

# def get_from_env(key: str, default: Any = None, *, type_cast: Optional[Callable[[Any], Any]] = None) -> Any:
#     value = os.getenv(key)
#     if value is None or value == "":
#         return default
#     if type_cast is not None:
#         return type_cast(value)
#     return value    

# def str_to_bool(value: Any) -> bool:
#     if not value:
#         return False
#     return str(value).lower() in ("y", "yes", "t", "true", "on", "1")

IS_PRO = get_from_env("IS_PRO", False, type_cast=str_to_bool)

WSGI = get_from_env("WSGI", False, type_cast=str_to_bool)

TEST = get_from_env("TEST", True, type_cast=str_to_bool)
SUFFIX = os.getenv("SUFFIX", "")

KAFKA_HOSTS = os.getenv("KAFKA_HOSTS", "localhost:29092")

KAFKA_PREFIX = os.getenv("KAFKA_PREFIX", "")

CLICKHOUSE_HOST = os.getenv("CLICKHOUSE_HOST", "localhost")
CLICKHOUSE_ALT_HOST = os.getenv("CLICKHOUSE_ALT_HOST", None)
CLICKHOUSE_PORT = get_from_env("CLICKHOUSE_PORT", 9000, type_cast=int)
CLICKHOUSE_HTTP_PORT = get_from_env("CLICKHOUSE_HTTP_PORT", 8123, type_cast=int)
CLICKHOUSE_CLUSTER = os.getenv("CLICKHOUSE_CLUSTER", "featbit_ch_cluster")
CLICKHOUSE_DATABASE = os.getenv("CLICKHOUSE_DATABASE", "featbit") + SUFFIX
CLICKHOUSE_SECURE = get_from_env("CLICKHOUSE_SECURE", False, type_cast=str_to_bool)
CLICKHOUSE_USER = os.getenv("CLICKHOUSE_USER", "default")
CLICKHOUSE_PASSWORD = os.getenv("CLICKHOUSE_PASSWORD", "")
CLICKHOUSE_CA = os.getenv("CLICKHOUSE_CA", None)
CLICKHOUSE_VERIFY = get_from_env("CLICKHOUSE_VERIFY", True, type_cast=str_to_bool)
CLICKHOUSE_CONN_POOL_MIN = get_from_env("CLICKHOUSE_CONN_POOL_MIN", 20, type_cast=int)
CLICKHOUSE_CONN_POOL_MAX = get_from_env("CLICKHOUSE_CONN_POOL_MAX", 1000, type_cast=int)
CLICKHOUSE_ENABLE_STORAGE_POLICY = get_from_env("CLICKHOUSE_ENABLE_STORAGE_POLICY", False, type_cast=str_to_bool)
CLICKHOUSE_KAFKA_HOSTS = os.getenv("CLICKHOUSE_KAFKA_HOSTS", "kafka:9092")
CLICKHOUSE_REPLICATION = get_from_env("CLICKHOUSE_REPLICATION", True, type_cast=str_to_bool)



MONGO_URI = os.getenv("MONGO_URI", "mongodb://admin:password@localhost:27017")
MONGO_DB = os.getenv("MONGO_INITDB_DATABASE", "featbit")

@click.command(name='migrate-database')
@click.option("--upto",
              default=9999,
              help="Database state will be brought to the state after that migration.")
@click.option("--check",
              is_flag=True,
              help="Exits with a non-zero status if unapplied migrations exist.")
@click.option("--plan",
              is_flag=True,
              help="Shows a list of the migration actions that will be performed.")
@click.option("--print-sql",
              is_flag=True,
              help="Use with --plan or --check. Also prints SQL for each migration to be applied.")
@click.pass_context
def migratedatabase(ctx, upto, check, plan, print_sql):
    """Featbit Data Analytics Database Migrator"""
    if IS_PRO:
        migrate_ch(upto, check, plan, print_sql)
        migrate_mongo()
    else:
        migrate_mongo()

def configure_logger():
    logging.basicConfig(level=logging.INFO, format='[%(asctime)s] %(levelname)s in %(module)s: %(message)s')

configure_logger()

if __name__ == '__main__':
    migratedatabase()

