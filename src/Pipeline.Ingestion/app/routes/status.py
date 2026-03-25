from fastapi import APIRouter
from pydantic import BaseModel

router = APIRouter()

class StatusResponse(BaseModel):
    status: str
    message: str

@router.get("/{seed_id}")
async def get_seed_status(seed_id: str) -> StatusResponse:
    # Future: look up real status from DB
    return StatusResponse(status="completed", message="Seed document processed successfully.")
