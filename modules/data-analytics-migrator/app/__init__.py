# from logging.config import dictConfig
# from typing import List, Tuple



# from app.extensions import get_mongodb
# from app.setting import (DEFAULT_LOGGING_CONFIG, IS_PRO, MONGO_URI)

# __app = None



    

#     if IS_PRO:
#         from app.commands import migrate_clickhouse
#         __app.cli.add_command(migrate_clickhouse, name='migrate-database')
#     else:
#         get_mongodb(__app, MONGO_URI)
#         from app.commands import migrate_mongodb
#         __app.cli.add_command(migrate_mongodb, name='migrate-database')

#     return __app


# def get_app(config_name='default') -> :
#     if __app is None:
#         _create_app(config_name)
#     return __app  # type: ignore
