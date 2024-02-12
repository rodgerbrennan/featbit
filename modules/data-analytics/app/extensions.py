# from flask_apscheduler import APScheduler
from aiocache import Cache

from pymongo import MongoClient

__scheduler = None

__cache = None

__mongodb = None


# def get_scheduler():
#     global __scheduler
#     if __scheduler is None:
#         __scheduler = APScheduler()
#     return __scheduler


def get_cache(config={}):
    global __cache
    if __cache is None:
        # __cache = Cache(config=config)
        __cache = Cache()
    return __cache


def get_mongodb(app=None, uri=None):
    global __mongodb
    if __mongodb is None and app is not None and uri is not None:
        __mongodb = MongoClient(app, uri=uri)
    return __mongodb
