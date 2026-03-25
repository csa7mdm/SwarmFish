from contextlib import asynccontextmanager
from fastapi import FastAPI
from .routes import seeds, status

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup: could load spacy model here if we want to front-load it
    yield
    # Shutdown
    pass

app = FastAPI(
    title="SwarmFish Ingestion Pipeline",
    description="Seed document ingestion for SwarmFish AI Simulation",
    version="0.1.0",
    lifespan=lifespan
)

app.include_router(seeds.router, prefix="/seeds", tags=["seeds"])
app.include_router(status.router, prefix="/status", tags=["status"])

@app.get("/health")
async def health_check():
    return {"status": "ok"}
