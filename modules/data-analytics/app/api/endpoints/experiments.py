import json


from fastapi import APIRouter, Response, status, HTTPException
from fastapi.responses import JSONResponse
from starlette.requests import Request

from app.experimentation.models.experiment import (Experiment,
                                                   analyze_experiment)
from app.extensions import get_cache
from app.utils import to_md5_hexdigest

from app.extensions import get_cache

import logging

logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

router = APIRouter()

@router.post('/results')
async def get_result(request: Request):
    json_str = await request.body()
    try:
        if not json_str:
            raise ValueError('post body is empty')
        cache_key = to_md5_hexdigest(json_str)
        data = await get_cache().get(cache_key)
        if not data:
            data = analyze_experiment(Experiment.from_properties(json.loads(json_str)))
            await get_cache().set(cache_key, data, ttl=10)
        return JSONResponse({"code":200, "error":'', "data":data})
    except Exception as e:
        logger.exception('unexpected error occurs: %s' % str(e))
        raise HTTPException(status_code=500, detail=str(e))
