from typing import Any, Union
import json

from fastapi import APIRouter, Response, status, HTTPException
from fastapi.encoders import jsonable_encoder
from fastapi.responses import JSONResponse
from starlette.requests import Request
from app.setting import settings
from app.utils import to_md5_hexdigest
from app.mainapp.models.statistics import (EndUserParams, EndUserStatistics, FeatureFlagIntervalStatistics, IntervalParams)

from app.clickhouse.models.event import bulk_create_events as bulk_create_events_ch
from app.mongodb.models.event import bulk_create_events as bulk_create_events_mongod
from app.extensions import get_cache

import logging

logging.basicConfig(level=logging.ERROR)
logger = logging.getLogger(__name__)

router = APIRouter()

@router.post('/')
async def create_events(request: Request):
    # this api is only for internal test, not use in prod
    json_str = await request.body()
    try:
        if not json_str:
            raise ValueError('post body is empty')
        _create_events(json_str)
        return {"code":200, "error":'', "data":{}}
    except Exception as e:
        logger.exception('unexpected error occurs: %s' % str(e))
        raise HTTPException(status_code=500, detail=str(e))


def _create_events(json_events: Union[str, bytes]) -> None:
    events = json.loads(json_events)
    if settings.IS_PRO:
        bulk_create_events_ch(events)
    else:
        bulk_create_events_mongod(events)


@router.post('/stat/{event}')
async def get_event_stat(event: str, request: Request) -> JSONResponse:
    json_str = await request.body()
    try:
        if not json_str:
            raise ValueError('post body is empty')
        cache_key = to_md5_hexdigest(json_str)
        data = await get_cache().get(cache_key)
        if not data:
            params = json.loads(json_str)
            if event == 'featureflag':
                logger.debug('FeatureFlagIntervalStatistics')
                data = FeatureFlagIntervalStatistics(IntervalParams.from_properties(params)).get_results()
                logger.debug(data)
            elif event == 'enduser':
                logger.debug('EndUserStatistics')
                data = EndUserStatistics(EndUserParams.from_properties(params)).get_results()
                logger.debug(data)
            else:
                raise NotImplementedError('event not supported')
            await get_cache().set(cache_key, data, ttl=1)
        return JSONResponse({"code":200, "error":'', "data":data})
    except Exception as e:
        logger.exception('unexpected error occurs: %s' % str(e))
        raise HTTPException(status_code=500, detail=str(e))
    
