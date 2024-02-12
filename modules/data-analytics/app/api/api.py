from fastapi import APIRouter

from app.api.endpoints import health, events, experiments

api_router = APIRouter()
api_router.include_router(health.router, prefix="/health", tags=["health"])
api_router.include_router(events.router, prefix="/api/events", tags=["events"])
api_router.include_router(experiments.router, prefix="/api/expt", tags=["expt"])