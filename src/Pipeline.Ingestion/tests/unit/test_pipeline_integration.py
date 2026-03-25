import pytest
import uuid
from fastapi.testclient import TestClient
from app.main import app

client = TestClient(app)

def test_health_check():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}

@pytest.mark.asyncio
async def test_full_pipeline_mocked(monkeypatch):
    """
    Run the full pipeline on a dummy fixture document,
    mocking the heavy external components (spaCy, LLM).
    """
    
    # Mock extractor
    async def mock_extract(text):
        from app.models import Entity
        return [Entity(id="ent_1", label="PERSON", name="Mock Person")], []
        
    monkeypatch.setattr("app.services.extractor.extract", mock_extract)
    
    # Mock persona gen
    async def mock_generate(*args, **kwargs):
        from app.models import Persona
        return [Persona(
            id=uuid.uuid4(),
            name="Mock Agent",
            backstory="Mock backstory",
            traits=["mock"],
            emotionalBaseline=0.5,
            socialInfluence=0.5,
            knownEntities=["ent_1"]
        )]
        
    monkeypatch.setattr("app.services.persona_gen.generate", mock_generate)
    
    # Send request
    doc_id = str(uuid.uuid4())
    payload = {
        "id": doc_id,
        "title": "Mock Title",
        "raw_content": "This is a 1000 word mock document " * 100,
        "type": "Custom",
        "metadata": {}
    }
    
    response = client.post("/seeds", json=payload)
    
    assert response.status_code == 200
    data = response.json()
    assert data["entity_count"] == 1
    assert len(data["persona_ids"]) == 1
