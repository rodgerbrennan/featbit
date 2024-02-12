from pymongo import MongoClient
from app.extensions import get_mongodb
from app.setting import settings


def get_db():
    try:
        app=None
        pymongo = get_mongodb(app, settings.MONGO_URI)
        pymongo.cx.server_info()  # type: ignore
        db = pymongo.cx[Setings.MONGO_DB]  # type: ignore
    except:
        pymongo = MongoClient(settings.MONGO_URI)
        db = pymongo[settings.MONGO_DB]
    return db
