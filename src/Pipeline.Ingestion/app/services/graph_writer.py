import httpx
from ..models import Entity, Relationship, Persona, settings

async def bulk_write(entities: list[Entity], relationships: list[Relationship], personas: list[Persona]):
    """
    Writes entities, relationships, and personas to KuzuDB via the .NET API Gateway.
    MOCK IMPLEMENTATION: Logs payloads and returns success. Do not call real KuzuDB.
    """
    print(f"Mocking graph write to {settings.api_url}/internal/graph/bulk")
    
    payload = {
        "nodes": [e.model_dump() for e in entities] + [p.model_dump(mode='json') for p in personas],
        "edges": [r.model_dump() for r in relationships]
    }
    
    print(f"Would write: {len(payload['nodes'])} nodes, {len(payload['edges'])} edges")
    
    # In a real app we would do:
    # async with httpx.AsyncClient() as client:
    #     response = await client.post(f"{settings.api_url}/internal/graph/bulk", json=payload)
    #     response.raise_for_status()

    # Just mock success for now
    return True
