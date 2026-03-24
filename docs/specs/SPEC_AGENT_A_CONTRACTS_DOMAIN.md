# Spec: Agent A — Core.Contracts + Core.Domain
> Read AGENTS.md at repo root first. This spec is your complete implementation brief.

## Your role
You are the **foundation layer agent**. Everything else depends on what you build.
No other agent can make a meaningful start without the contracts and domain you define.
Finish Phase 1 before any other agent begins Phase 2.

## Phase 1 — Core.Contracts (complete this first, block all other agents until done)

### Project setup
```xml
<!-- src/Core.Contracts/Core.Contracts.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <!-- ZERO NuGet dependencies allowed here -->
</Project>
```

### Interfaces to implement

**IAgent.cs**
```csharp
public interface IAgent
{
    Guid Id { get; }
    string Persona { get; }           // JSON-serialised personality blob
    AgentStatus Status { get; }
    Task<AgentEvent> ProcessTickAsync(SimulationTick tick, CancellationToken ct);
}

public enum AgentStatus { Idle, Active, Suppressed, Terminated }
```

**ISimulationTick.cs**
```csharp
public record SimulationTick(
    int Round,
    DateTimeOffset SimulatedTime,
    IReadOnlyList<AgentEvent> PreviousEvents,
    SimulationContext Context
);

public record AgentEvent(
    Guid AgentId,
    string EventType,      // "spoke", "moved", "reacted", "silent"
    string Payload,        // JSON blob
    DateTimeOffset Timestamp
);
```

**IGraphStore.cs**
```csharp
public interface IGraphStore : IAsyncDisposable
{
    Task<IReadOnlyList<GraphNode>> QueryAsync(string cypher, CancellationToken ct);
    Task UpsertNodeAsync(GraphNode node, CancellationToken ct);
    Task UpsertEdgeAsync(GraphEdge edge, CancellationToken ct);
    Task<GraphNode?> GetNodeByIdAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<GraphNode>> GetNeighboursAsync(string nodeId, int depth, CancellationToken ct);
}

public record GraphNode(string Id, string Label, IReadOnlyDictionary<string, object> Properties);
public record GraphEdge(string SourceId, string TargetId, string RelationType, IReadOnlyDictionary<string, object> Properties);
```

**IMemoryStore.cs**
```csharp
public interface IMemoryStore
{
    Task<AgentMemory> GetMemoryAsync(Guid agentId, CancellationToken ct);
    Task AppendMemoryAsync(Guid agentId, MemoryEntry entry, CancellationToken ct);
    Task<IReadOnlyList<MemoryEntry>> SearchMemoryAsync(Guid agentId, string query, int topK, CancellationToken ct);
}

public record AgentMemory(Guid AgentId, IReadOnlyList<MemoryEntry> Entries);
public record MemoryEntry(string Content, DateTimeOffset CreatedAt, MemoryEntryType Type, float Relevance = 1.0f);
public enum MemoryEntryType { Observation, Reflection, Reaction, SeedFact }
```

**ISeedDocument.cs**
```csharp
public interface ISeedDocument
{
    Guid Id { get; }
    string Title { get; }
    string RawContent { get; }
    SeedDocumentType Type { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
}

public enum SeedDocumentType { NewsArticle, PolicyDocument, FinancialReport, Novel, Custom }
```

**IPredictionReport.cs**
```csharp
public interface IPredictionReport
{
    Guid SimulationId { get; }
    string Summary { get; }
    IReadOnlyList<PredictionFinding> Findings { get; }
    IReadOnlyList<TimelineEvent> Timeline { get; }
    float ConfidenceScore { get; }
}

public record PredictionFinding(string Category, string Description, float Confidence, IReadOnlyList<string> SupportingAgentIds);
public record TimelineEvent(DateTimeOffset SimulatedAt, string Description, IReadOnlyList<Guid> InvolvedAgents);
```

**ISimulationRunner.cs**
```csharp
public interface ISimulationRunner
{
    Task<Guid> StartAsync(SimulationConfig config, CancellationToken ct);
    Task PauseAsync(Guid simulationId, CancellationToken ct);
    Task ResumeAsync(Guid simulationId, CancellationToken ct);
    Task StopAsync(Guid simulationId, CancellationToken ct);
    IAsyncEnumerable<SimulationProgress> WatchAsync(Guid simulationId, CancellationToken ct);
}

public record SimulationConfig(
    Guid SeedDocumentId,
    int AgentCount,
    int MaxRounds,
    string PredictionQuery,
    SimulationMode Mode
);

public enum SimulationMode { Standard, HighFidelity, FastSweep }

public record SimulationProgress(
    Guid SimulationId,
    int CurrentRound,
    int TotalRounds,
    SimulationState State,
    DateTimeOffset Timestamp
);

public enum SimulationState { Initialising, Running, Paused, Completed, Failed }
```

## Phase 2 — Core.Domain

### Domain entities to model

**AgentPersona** (value object, immutable)
- Name, backstory, traits (List<string>), emotionalBaseline (float 0-1), socialInfluence (float 0-1)
- Must be serialisable to/from JSON
- Include a static `GenerateDefault(int seed)` for testing

**Simulation** (aggregate root)
- Holds SimulationConfig, current state machine, list of participating AgentIds
- State transitions: Created → Initialising → Running → Paused → Completed | Failed
- Raises domain events on every state transition

**SeedDocument** (entity, implements ISeedDocument)
- Concrete implementation of ISeedDocument
- Add `ExtractedEntities` property: `IReadOnlyList<string>` (populated after ingestion)
- Add `EmbeddingVector` property: `float[]?`

**SimulationRound** (value object)
- Immutable snapshot of one tick: round number, all AgentEvents, duration
- Include aggregate stats: eventCount, uniqueAgents, dominantEventType

### Domain events
Define a base `DomainEvent` record and these concrete events:
- `SimulationStarted`, `SimulationCompleted`, `SimulationFailed`
- `AgentActivated`, `AgentSuppressed`
- `RoundCompleted`

### Validation
All domain constructors must validate invariants and throw `DomainException` (define this) on violation.
Examples: AgentCount must be between 2 and 100_000. PredictionQuery cannot be empty.

## Tests required (in tests/unit/Core.Domain.Tests/)
- All interface implementations compile and satisfy contracts
- AgentPersona serialisation roundtrip
- Simulation state machine — all valid + invalid transitions
- SimulationConfig validation — boundary conditions
- SeedDocument construction with metadata

## Done criteria
- [ ] All interfaces in Core.Contracts compile with zero warnings
- [ ] All domain entities in Core.Domain compile with zero warnings
- [ ] 100% of domain entity constructors have validation
- [ ] Unit tests pass: `dotnet test tests/unit/Core.Domain.Tests`
- [ ] AGENTS.md added to both project directories documenting internal conventions
