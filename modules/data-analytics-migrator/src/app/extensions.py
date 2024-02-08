from flask_pymongo import PyMongo


__mongodb = None


def get_mongodb(app=None, uri=None):
    global __mongodb
    if __mongodb is None and app is not None and uri is not None:
        __mongodb = PyMongo(app, uri=uri)
    return __mongodb
