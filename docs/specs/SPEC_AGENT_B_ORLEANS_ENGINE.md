# Spec: Agent B — Agents.Orleans + Simulation.Engine
> Read root AGENTS.md first. Read SPEC_AGENT_A first — your Grain interfaces extend Core.Contracts.
> Do not start Phase 2 until Agent A's Core.Contracts are merged.

## Your role
You build the **heart of the engine**: the Orleans grain model that represents every simulated agent,
and the simulation loop that drives ticks across all agents in parallel.

## Phase 1 — Agents.Orleans

### Project setup
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Orleans.Core" Version="8.*" />
    <PackageReference Include="Microsoft.Orleans.Server" Version="8.*" />
    <PackageReference Include="Microsoft.Orleans.Persistence.Memory" Version="8.*" />
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />
    <ProjectReference Include="../Core.Contracts/Core.Contracts.csproj" />
    <ProjectReference Include="../Memory.Zep/Memory.Zep.csproj" />
    <ProjectReference Include="../Graph.KuzuDB/Graph.KuzuDB.csproj" />
  </ItemGroup>
</Project>
```

### IAgentGrain interface
```csharp
public interface IAgentGrain : IGrainWithGuidKey
{
    Task InitialiseAsync(AgentPersona persona, Guid simulationId);
    Task<AgentEvent> ProcessTickAsync(SimulationTick tick);
    Task<AgentPersona> GetPersonaAsync();
    Task SuppressAsync();
    Task ReactivateAsync();
}
```

### AgentGrain implementation
The grain must:
1. On `InitialiseAsync`: persist persona to grain state, seed Zep memory with persona facts
2. On `ProcessTickAsync`:
   a. Retrieve relevant memories from IMemoryStore (top-5 semantic search)
   b. Retrieve agent's graph context from IGraphStore (2-hop neighbourhood)
   c. Build a Semantic Kernel prompt with: persona + memories + graph context + tick events
   d. Call LLM via SK (streaming, collect full response)
   e. Parse response into AgentEvent
   f. Append event as new MemoryEntry
   g. Return AgentEvent
3. Handle `SuppressAsync`/`ReactivateAsync` by updating grain state

**Prompt template for ProcessTickAsync** (put in `Resources/agent_tick_prompt.txt`):
```
You are {persona.Name}. {persona.Backstory}
Your traits: {persona.Traits}

Recent memories (most relevant to current situation):
{memories}

Your social connections and context:
{graphContext}

Current simulation round {tick.Round} — simulated time: {tick.SimulatedTime}
Recent events in the world:
{previousEvents}

Based on all of the above, decide what you do or say this round.
Respond as a JSON object:
{
  "eventType": "spoke|moved|reacted|silent",
  "payload": "<your action or statement, max 200 words>",
  "targetAgentId": "<guid or null if not directed>"
}
Respond ONLY with valid JSON. No preamble.
```

### Grain state persistence
Use Orleans in-memory persistence for development. Design the state class to be swappable:
```csharp
[GenerateSerializer]
public class AgentGrainState
{
    public AgentPersona? Persona { get; set; }
    public Guid SimulationId { get; set; }
    public AgentStatus Status { get; set; }
    public int TicksProcessed { get; set; }
}
```

### Performance critical: batch processing
Implement `IAgentBatchCoordinator` grain:
- Accepts a list of AgentIds + SimulationTick
- Fans out `ProcessTickAsync` to all agents using `Task.WhenAll` in configurable batches of 50
- Collects all AgentEvents and returns them
- Tracks per-batch latency in a Meter (`System.Diagnostics.Metrics`)

## Phase 2 — Simulation.Engine

### Project setup
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Orleans.Client" Version="8.*" />
  <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.*" />
  <ProjectReference Include="../Core.Contracts/Core.Contracts.csproj" />
  <ProjectReference Include="../Agents.Orleans/Agents.Orleans.csproj" />
</ItemGroup>
```

### SimulationOrchestrator (implements ISimulationRunner)
State machine:
```
Created → Initialising (spawn agents) → Running (tick loop) → Paused | Completed | Failed
```

Tick loop algorithm:
```csharp
while (round <= config.MaxRounds && !cancellationToken.IsCancellationRequested)
{
    var tick = BuildTick(round, previousEvents, context);
    var batchEvents = await batchCoordinator.ProcessBatchAsync(activeAgentIds, tick, ct);
    previousEvents = batchEvents;
    round++;
    await progressChannel.WriteAsync(new SimulationProgress(...));
    await ApplyHerdBiasCorrection(batchEvents);  // see below
}
```

### Herd bias correction (important!)
MiroFish has a known issue: LLM agents converge on consensus too fast (herd behaviour).
Implement `HerdBiasCorrector`:
- After each tick, calculate a "diversity score" = unique event types / total events
- If diversity score < 0.3, randomly suppress 20% of the most-aligned agents for 1 round
- Log suppression decisions to ILogger with structured data

### Progress streaming
Use `System.Threading.Channels.Channel<SimulationProgress>` to stream progress.
The channel is consumed by Api.Gateway via ISimulationRunner.WatchAsync (IAsyncEnumerable).

### Agent initialisation strategy
```csharp
// Spawn agents in parallel batches of 200
var batches = agentPersonas.Chunk(200);
foreach (var batch in batches)
{
    await Task.WhenAll(batch.Select(p => 
        clusterClient.GetGrain<IAgentGrain>(p.Id).InitialiseAsync(p, simulationId)));
}
```

## Tests required
- AgentGrain: mock IMemoryStore + IGraphStore + SK kernel, verify ProcessTickAsync produces valid AgentEvent
- AgentGrain: verify state persists across grain deactivation/reactivation
- SimulationOrchestrator: state machine transitions (all valid paths)
- HerdBiasCorrector: diversity score calculation, suppression trigger at threshold
- Batch coordinator: fan-out with mixed success/failure results

## Done criteria
- [ ] AgentGrain activates, processes a tick, persists state — verified by integration test
- [ ] Batch coordinator fans out 1000 agents in parallel without deadlock
- [ ] Herd bias correction triggers and logs correctly
- [ ] Simulation state machine covers all transitions with tests
- [ ] Metrics emitted for tick latency, batch size, diversity score
