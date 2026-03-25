from ..models import Entity, Persona, SeedDocument

async def seed_all(personas: list[Persona], entities: list[Entity], document: SeedDocument):
    """
    Seeds initial Zep memories for each generated persona.
    For MVP, we just create a few fake memory entries linking them to the seed document context.
    """
    
    for persona in personas:
        # In a real app we'd call the Memory.Zep gateway API here:
        # POST {settings.api_url}/internal/memory/{persona.id}
        
        known_entity_names = [e.name for e in entities if e.id in persona.knownEntities]
        entity_str = ", ".join(known_entity_names) if known_entity_names else "none specifically"
        
        print(f"Seeding memory for Agent {persona.id} ({persona.name}). Traits: {persona.traits}")
        print(f"  -> Recalls entities: {entity_str}")
        print(f"  -> Background fact: I am aware of the events in '{document.title}'")

    return True
