import pytest
import uuid
from app.services.persona_gen import generate
from app.models import Entity, Relationship, Persona

@pytest.mark.asyncio
async def test_persona_gen_validates_json(monkeypatch):
    """
    Mock the OpenAI client to return a valid JSON list of personas, 
    and verify Pydantic parses it correctly.
    """
    
    mock_json = '''
    [
      {
        "id": "123e4567-e89b-12d3-a456-426614174000",
        "name": "Alice Tester",
        "backstory": "Alice is a tester. She tests things.",
        "traits": ["meticulous", "curious"],
        "emotionalBaseline": 0.5,
        "socialInfluence": 0.8,
        "knownEntities": ["entity_1"]
      }
    ]
    '''
    
    class MockMessage:
        content = mock_json
        
    class MockChoice:
        message = MockMessage()
        
    class MockCompletion:
        choices = [MockChoice()]
        
    class MockCreate:
        async def create(self, **kwargs):
            return MockCompletion()
            
    class MockChat:
        completions = MockCreate()
        
    class MockClient:
        chat = MockChat()
        
    # Patch the global client instance in the module
    monkeypatch.setattr("app.services.persona_gen.client", MockClient())
    
    entities = [Entity(id="entity_1", label="PERSON", name="Bob")]
    relationships = []
    
    personas = await generate(entities, relationships, count=1, seed_content="Test")
    
    assert len(personas) == 1
    assert isinstance(personas[0], Persona)
    assert personas[0].name == "Alice Tester"
    assert "meticulous" in personas[0].traits
    assert personas[0].emotionalBaseline == 0.5
