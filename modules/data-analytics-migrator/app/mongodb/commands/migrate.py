import logging
from app.mongodb.db import get_db
from app.setting import MONGO_DB_EVENTS_COLLECTION
from pymongo import ASCENDING


logger = logging.getLogger(__name__)

def migrate() -> None:

    logger.info("Migration in MongoDB")
    db = get_db()
    if MONGO_DB_EVENTS_COLLECTION not in db.list_collection_names():
        db.Events.create_index([("event", ASCENDING), ("env_id", ASCENDING), ("distinct_id", ASCENDING), ("timestamp", ASCENDING)])
        logger.info("MongoDB migrations up to date!")
    logger.info("✅ Migration successful")
