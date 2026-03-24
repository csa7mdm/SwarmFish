# Spec: Agent D — Memory.Zep + Pipeline.Ingestion
> Read root AGENTS.md first. Memory.Zep can start in parallel with Agent A Phase 2.
> Pipeline.Ingestion starts after Core.Contracts are merged.

## Your role
You build two things that bookend the pipeline:
1. **Memory.Zep** — the .NET adapter giving every agent persistent, searchable temporal memory
2. **Pipeline.Ingestion** — the Python microservice that turns raw seed documents into
   personas, entity graphs, and seeded memories ready for simulation

## Part 1 — Memory.Zep (.NET)

### Project setup
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Zep.Client" Version="*" />  <!-- Official Zep .NET SDK -->
    <PackageReference Include="Microsoft.Extensions.Options" Version="9.*" />
    <ProjectReference Include="../Core.Contracts/Core.Contracts.csproj" />
  </ItemGroup>
</Project>
```

### ZepMemoryStore : IMemoryStore
Map our `IMemoryStore` contract onto Zep's API:
- Each agent has a Zep "session" — session ID = `agentId.ToString()`
- `AppendMemoryAsync` → Zep `AddMemoryAsync` with role="assistant", content=entry.Content
- `SearchMemoryAsync` → Zep `SearchMemoryAsync` (semantic search, return topK)
- `GetMemoryAsync` → Zep `GetMemoryAsync` (returns full session history)

**Important**: Zep's free tier has rate limits. Implement a `SlidingWindowRateLimiter` wrapper:
- Max 100 requests/second across all agents
- Queue overflow requests with backpressure (Channel<Func<Task>> with bounded capacity 1000)

### ZepMemoryOptions
```csharp
public class ZepMemoryOptions
{
    public string ApiKey { get; set; } = "";
    public int MaxMemoriesPerAgent { get; set; } = 500;
    public int SearchTopK { get; set; } = 5;
    public int RateLimitRps { get; set; } = 100;
}
```

### Session lifecycle management
Implement `IZepSessionManager`:
- `EnsureSessionAsync(Guid agentId)` — creates Zep session if not exists, idempotent
- `DeleteSessionAsync(Guid agentId)` — cleanup after simulation ends
- `PurgeSimulationAsync(Guid simulationId, IEnumerable<Guid> agentIds)` — batch cleanup

## Part 2 — Pipeline.Ingestion (Python)

### Service structure
```
src/Pipeline.Ingestion/
  app/
    main.py              # FastAPI app entry point
    routes/
      seeds.py           # POST /seeds — upload seed document
      status.py          # GET /seeds/{id}/status
    services/
      extractor.py       # Entity + relationship extraction
      persona_gen.py     # Generate agent personas from seed
      embedder.py        # Generate embeddings
      graph_writer.py    # Write to KuzuDB via REST
      memory_seeder.py   # Seed initial Zep memories
    models.py            # Pydantic models
  pyproject.toml
  .env.example
```

### Ingestion pipeline (sequential steps for one seed document)
```python
async def process_seed(document: SeedDocument) -> IngestionResult:
    # Step 1: Extract entities + relationships with spaCy + LLM assist
    entities, relationships = await extractor.extract(document.raw_content)
    
    # Step 2: Generate N agent personas grounded in the seed world
    # N = config.agent_count (passed in request)
    personas = await persona_gen.generate(
        entities=entities,
        relationships=relationships,
        count=config.agent_count,
        seed_content=document.raw_content
    )
    
    # Step 3: Embed the full document (chunked, 512 token windows, 50 token overlap)
    chunks = chunk_text(document.raw_content, size=512, overlap=50)
    embeddings = await embedder.embed_batch(chunks)
    
    # Step 4: Write entities + relationships to KuzuDB via HTTP
    await graph_writer.bulk_write(entities, relationships, personas)
    
    # Step 5: Seed each agent's Zep memory with 3-5 relevant facts from document
    await memory_seeder.seed_all(personas, entities, document)
    
    return IngestionResult(persona_ids=[p.id for p in personas], entity_count=len(entities))
```

### Persona generation prompt
Use this template in `persona_gen.py`. Send to LLM, parse JSON response:
```
You are building agents for a social simulation grounded in this document.

Document summary: {summary}
Key entities: {entity_list}
Key relationships: {relationship_list}

Generate {count} distinct agent personas. Each persona should be:
- Believable within this world
- Have a distinct personality (not all agree with each other)
- Have a plausible relationship to the entities in the document
- Vary in: age, social status, political leaning, emotional volatility

Return a JSON array of objects, each with:
  id, name, backstory (2-3 sentences), traits (list of 3-5), 
  emotionalBaseline (0.0-1.0), socialInfluence (0.0-1.0),
  knownEntities (list of entity ids from above)

Respond ONLY with valid JSON. No preamble or explanation.
```

### Entity extraction
Use spaCy `en_core_web_trf` for NER, then LLM pass for relationship extraction:
```python
import spacy
nlp = spacy.load("en_core_web_trf")

def extract_entities(text: str) -> list[Entity]:
    doc = nlp(text)
    return [Entity(id=make_id(ent.text), label=ent.label_, name=ent.text) 
            for ent in doc.ents if ent.label_ in ("PERSON","ORG","GPE","EVENT","LAW")]
```

### pyproject.toml
```toml
[project]
name = "swarmfish-ingestion"
requires-python = ">=3.11,<3.13"
dependencies = [
    "fastapi>=0.110",
    "uvicorn[standard]>=0.29",
    "spacy>=3.7",
    "openai>=1.0",      # OpenAI-compatible client (works with any LLM)
    "httpx>=0.27",
    "pydantic>=2.0",
    "python-dotenv>=1.0"
]

[tool.uv.sources]
# spacy model installed separately: uv run python -m spacy download en_core_web_trf
```

### Integration point: calling KuzuDB
The Python service calls the .NET API Gateway to write to KuzuDB.
Do NOT try to use KuzuDB from Python directly — the Rust binding is in .NET.
```python
# graph_writer.py
async def bulk_write(entities, relationships, personas):
    async with httpx.AsyncClient() as client:
        await client.post(f"{settings.API_URL}/internal/graph/bulk", json={
            "nodes": [e.model_dump() for e in entities + personas],
            "edges": [r.model_dump() for r in relationships]
        })
```

## Tests required
**Memory.Zep:**
- ZepMemoryStore: mock Zep SDK, verify AppendMemory maps correctly
- Rate limiter: verify 100 rps limit with 200 concurrent requests
- Session manager: idempotent session creation

**Pipeline.Ingestion:**
- Extractor: verify spaCy pipeline on a 500-word fixture text
- Persona generator: verify JSON output matches expected schema for count=10
- Embedder: verify chunking logic (512/50 window) on long texts
- Integration test: full pipeline on a 1000-word fixture document

## Done criteria
- [ ] ZepMemoryStore implements IMemoryStore — verified by interface compliance test
- [ ] Rate limiter enforces 100 rps — verified by load test
- [ ] FastAPI service starts, accepts POST /seeds, runs pipeline
- [ ] Personas generated are valid JSON and match AgentPersona schema
- [ ] Entity extraction produces meaningful output on a news article fixture
