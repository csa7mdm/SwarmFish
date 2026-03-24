# Spec: Agent C — Graph.KuzuDB
> Read root AGENTS.md first. Can start in parallel with Agent A Phase 2 and Agent B.

## Your role
You build the **knowledge graph layer**: a .NET wrapper around KuzuDB that implements
`IGraphStore` from Core.Contracts. Every persona, relationship, and entity extracted
from seed documents lives here. GraphRAG retrieval runs through you.

## Why KuzuDB
KuzuDB is an embedded, columnar graph database written in Rust with a C API.
No separate server process. Zero-copy for in-process queries. Cypher-compatible query language.
For our use case (graph built once, read millions of times during simulation), it's the right call.

## Phase 1 — Native binding setup

### KuzuDB C API integration
KuzuDB ships a native library. Integrate it via P/Invoke:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.*" />
    <ProjectReference Include="../Core.Contracts/Core.Contracts.csproj" />
  </ItemGroup>
  <!-- Native KuzuDB library is bundled in /runtimes/ -->
</Project>
```

Create `Native/KuzuNative.cs` with P/Invoke declarations:
```csharp
internal static class KuzuNative
{
    private const string LibName = "kuzu";

    [DllImport(LibName)] internal static extern IntPtr kuzu_database_init(string dbPath, IntPtr config);
    [DllImport(LibName)] internal static extern void kuzu_database_destroy(IntPtr db);
    [DllImport(LibName)] internal static extern IntPtr kuzu_connection_init(IntPtr db);
    [DllImport(LibName)] internal static extern void kuzu_connection_destroy(IntPtr conn);
    [DllImport(LibName)] internal static extern IntPtr kuzu_connection_query(IntPtr conn, string query);
    [DllImport(LibName)] internal static extern void kuzu_query_result_destroy(IntPtr result);
    [DllImport(LibName)] internal static extern bool kuzu_query_result_is_success(IntPtr result);
    [DllImport(LibName)] internal static extern bool kuzu_query_result_has_next(IntPtr result);
    [DllImport(LibName)] internal static extern IntPtr kuzu_query_result_get_next(IntPtr result);
    // Add full set from KuzuDB C API docs
}
```

Wrap all P/Invoke in a `KuzuConnection` disposable class that manages handle lifetimes safely.

### Connection pooling
Implement `KuzuConnectionPool`:
- Fixed pool of N connections (configurable, default 8)
- `Channel<KuzuConnection>` for thread-safe lease/return
- `LeaseAsync(CancellationToken)` → returns a `IDisposable` scoped connection that auto-returns

## Phase 2 — IGraphStore implementation

### KuzuGraphStore : IGraphStore
```csharp
public sealed class KuzuGraphStore : IGraphStore
{
    // Schema initialisation on first open:
    // CREATE NODE TABLE IF NOT EXISTS Entity(id STRING, label STRING, properties STRING, PRIMARY KEY(id))
    // CREATE NODE TABLE IF NOT EXISTS Agent(id STRING, personaJson STRING, simulationId STRING, PRIMARY KEY(id))
    // CREATE REL TABLE IF NOT EXISTS KNOWS(FROM Agent TO Agent, weight FLOAT, context STRING)
    // CREATE REL TABLE IF NOT EXISTS MENTIONED_IN(FROM Entity TO Entity, source STRING)
    // CREATE REL TABLE IF NOT EXISTS HAS_TRAIT(FROM Agent TO Entity, strength FLOAT)
}
```

Implement all `IGraphStore` methods, mapping `GraphNode`/`GraphEdge` ↔ Kuzu rows.

### GraphRAG retrieval method
Add beyond the interface — `GetPersonaContextAsync(Guid agentId, int hopDepth = 2)`:
- Returns a structured string summary of the agent's graph neighbourhood
- Format: `"{agentName} knows: {list}. Relevant entities: {list}. Key relationships: {list}"`
- Used by AgentGrain to build the LLM context window

### Schema migration
Implement a simple `IGraphMigration` interface + `KuzuMigrationRunner`:
- Reads migration scripts from `/Migrations/*.cypher` in order
- Tracks applied migrations in a `_Migrations` node table
- Idempotent — safe to run on startup

## Phase 3 — Performance tuning

### Bulk import for seed data
The Python ingestion pipeline will call a REST endpoint to bulk-load extracted entities.
Implement `BulkImportAsync(IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges)`:
- Use KuzuDB's COPY FROM CSV for bulk load (fastest path)
- Write nodes/edges to temp CSV files, bulk import, clean up
- Should handle 100K nodes + 500K edges under 30 seconds

### Read-path caching
Wrap hot-path reads (GetNeighboursAsync, GetPersonaContextAsync) with
`System.Runtime.Caching.MemoryCache`:
- TTL: 60 seconds (simulation rounds are longer than this)
- Key: `{nodeId}:{depth}:{simulationId}`
- Invalidate on any UpsertNodeAsync or UpsertEdgeAsync for that node

## Tests required
- KuzuConnectionPool: concurrent lease/return under load (100 threads, 1000 operations)
- KuzuGraphStore: node CRUD, edge CRUD, multi-hop query
- GraphRAG retrieval: verify format and content for a known graph fixture
- BulkImport: 10K nodes + 50K edges, verify all written correctly
- Migration runner: applies migrations in order, skips already-applied

## Done criteria
- [ ] KuzuDB native library loaded and P/Invoke verified on Linux x64 + Windows x64
- [ ] IGraphStore fully implemented and tested
- [ ] BulkImport handles 100K nodes in under 30s (benchmark test included)
- [ ] Schema migrations run on startup without error
- [ ] Connection pool holds under concurrent load
