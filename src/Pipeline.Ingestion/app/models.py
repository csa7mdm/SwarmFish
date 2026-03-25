from uuid import UUID
from pydantic import BaseModel, Field
from pydantic_settings import BaseSettings, SettingsConfigDict

class Settings(BaseSettings):
    llm_api_key: str = ""
    llm_base_url: str = "https://api.openai.com/v1"
    llm_model_name: str = "gpt-4o"
    api_url: str = "http://localhost:5001"
    agent_count: int = 20

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

settings = Settings()

class SeedDocument(BaseModel):
    id: UUID
    title: str
    raw_content: str
    type: str = "Custom"
    metadata: dict[str, str] = Field(default_factory=dict)

class Entity(BaseModel):
    id: str
    label: str
    name: str

class Relationship(BaseModel):
    source_id: str
    target_id: str
    relation_type: str
    properties: dict = Field(default_factory=dict)

class Persona(BaseModel):
    id: UUID
    name: str
    backstory: str
    traits: list[str]
    emotionalBaseline: float
    socialInfluence: float
    knownEntities: list[str]

class IngestionResult(BaseModel):
    persona_ids: list[UUID]
    entity_count: int
