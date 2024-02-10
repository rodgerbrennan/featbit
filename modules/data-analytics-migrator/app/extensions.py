import pymongo


__mongodb = None


def get_mongodb(app=None, uri=None):
    global __mongodb
    if __mongodb is None and app is not None and uri is not None:
        __mongodb = pymongo(app, uri=uri)
    return __mongodb
