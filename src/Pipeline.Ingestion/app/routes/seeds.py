from fastapi import APIRouter
from ..models import SeedDocument, IngestionResult, settings
from ..services import extractor, persona_gen, embedder, graph_writer, memory_seeder

router = APIRouter()

@router.post("")
async def process_seed(document: SeedDocument) -> IngestionResult:
    """
    Full ingestion pipeline orchestrator logic:
    1. Extract entities & relationships
    2. Generate N personas
    3. Embed text chunks
    4. Write to GraphDB (mock)
    5. Seed memories (mock)
    """
    
    # 1. Extract
    entities, relationships = await extractor.extract(document.raw_content)
    
    # 2. Generate personas
    personas = await persona_gen.generate(
        entities=entities, 
        relationships=relationships, 
        count=settings.agent_count, 
        seed_content=document.raw_content
    )
    
    # 3. Embed text (mocked logic)
    chunks = embedder.chunk_text(document.raw_content, size=512, overlap=50)
    embeddings = await embedder.embed_batch(chunks)
    
    # 4. Write to graph
    await graph_writer.bulk_write(entities, relationships, personas)
    
    # 5. Seed memory
    await memory_seeder.seed_all(personas, entities, document)
    
    return IngestionResult(
        persona_ids=[p.id for p in personas], 
        entity_count=len(entities)
    )
