from fastapi import APIRouter,status
from fastapi.responses import JSONResponse

router = APIRouter()

@router.get('/liveness')
def get_liveness():
    data={'state': f'OK'}
    return JSONResponse(content=data, media_type="application/json", status_code=status.HTTP_200_OK)

@router.get('/readiness')
def get_readiness():
    data={'state': f'OK'}
    return JSONResponse(content=data, media_type="application/json", status_code=status.HTTP_200_OK)