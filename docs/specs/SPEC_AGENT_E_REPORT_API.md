# Spec: Agent E — Report.Agent + Api.Gateway
> Read root AGENTS.md first. Depends on Core.Contracts (Agent A) and ISimulationRunner (Agent B).
> Can scaffold project structure in parallel, but cannot wire up logic until B is done.

## Your role
You build the **output and coordination surface**: the ReportAgent that synthesises
simulation results into a prediction report, and the API Gateway that ties the whole
system together and exposes it to the frontend.

## Part 1 — Report.Agent

### Project setup
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />
    <PackageReference Include="Microsoft.SemanticKernel.Plugins.Core" Version="1.*" />
    <PackageReference Include="Microsoft.Orleans.Client" Version="8.*" />
    <ProjectReference Include="../Core.Contracts/Core.Contracts.csproj" />
    <ProjectReference Include="../Graph.KuzuDB/Graph.KuzuDB.csproj" />
    <ProjectReference Include="../Memory.Zep/Memory.Zep.csproj" />
  </ItemGroup>
</Project>
```

### ReportAgentService
This is NOT an Orleans grain — it's a regular service that runs after simulation completes.

```csharp
public class ReportAgentService
{
    public async Task<IPredictionReport> GenerateReportAsync(
        Guid simulationId,
        string predictionQuery,
        CancellationToken ct)
    {
        // Step 1: Gather post-simulation data
        var allEvents = await GetAllSimulationEventsAsync(simulationId, ct);
        var graphSummary = await graphStore.QueryAsync(PostSimSummaryQuery, ct);
        
        // Step 2: Build SK kernel with tools
        var kernel = BuildKernelWithTools();
        
        // Step 3: Run report generation via SK
        var result = await kernel.InvokePromptAsync(
            BuildReportPrompt(allEvents, graphSummary, predictionQuery), ct: ct);
        
        // Step 4: Parse + return structured report
        return ParseReport(result.ToString());
    }
}
```

### Semantic Kernel tool plugins
Register these SK plugins on the kernel:

**GraphQueryPlugin** — let the ReportAgent ask questions to the graph:
```csharp
[KernelFunction, Description("Query the simulation knowledge graph")]
public async Task<string> QueryGraphAsync(
    [Description("Cypher query")] string query) { ... }
```

**EventAnalysisPlugin** — aggregate analysis over all simulation events:
```csharp
[KernelFunction, Description("Get event frequency analysis")]
public string GetEventFrequency(string eventType) { ... }

[KernelFunction, Description("Get the most influential agents by event count")]
public string GetTopAgents(int count) { ... }
```

**MemoryQueryPlugin** — query any agent's memories:
```csharp
[KernelFunction, Description("Search an agent's memories for relevant content")]
public async Task<string> SearchAgentMemoryAsync(string agentId, string query) { ... }
```

### ReportAgent interactive mode
After initial report generation, support follow-up questions from the user.
Implement `ChatWithReportAsync(Guid simulationId, string userMessage, IEnumerable<ChatMessage> history)`:
- Maintains chat history within the session
- Has access to all three plugins
- Uses SK's ChatCompletionService for multi-turn

### Report prompt template (`Resources/report_prompt.txt`)
```
You are a senior analyst reviewing results from a multi-agent social simulation.
The simulation was run to answer: "{predictionQuery}"

Simulation ran for {roundCount} rounds with {agentCount} agents.

Event summary:
{eventSummary}

Knowledge graph state after simulation:
{graphSummary}

Use the available tools to query the graph and agent memories for deeper evidence.
Then produce a structured prediction report covering:
1. SUMMARY: 2-3 sentence answer to the prediction query
2. KEY FINDINGS: 3-5 specific findings with confidence scores (0.0-1.0)
3. TIMELINE: Chronological list of pivotal simulation moments
4. DISSENTING VIEWS: Notable minority positions from agents who disagreed with the majority
5. CONFIDENCE: Overall confidence score with reasoning

Format as JSON matching the IPredictionReport schema.
```

## Part 2 — Api.Gateway

### Project setup
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="9.*" />
    <PackageReference Include="Microsoft.Orleans.Client" Version="8.*" />
    <PackageReference Include="Scalar.AspNetCore" Version="*" />  <!-- OpenAPI UI -->
    <PackageReference Include="Microsoft.AspNetCore.RateLimiting" Version="9.*" />
    <ProjectReference Include="../Core.Contracts/Core.Contracts.csproj" />
    <ProjectReference Include="../Simulation.Engine/Simulation.Engine.csproj" />
    <ProjectReference Include="../Report.Agent/Report.Agent.csproj" />
    <ProjectReference Include="../Graph.KuzuDB/Graph.KuzuDB.csproj" />
  </ItemGroup>
</Project>
```

### REST endpoints (minimal API, group by resource)

**Simulations**
```
POST   /api/simulations            → start simulation, returns {simulationId}
GET    /api/simulations/{id}       → get simulation status + config
POST   /api/simulations/{id}/pause
POST   /api/simulations/{id}/resume
DELETE /api/simulations/{id}       → stop + cleanup
```

**Seeds**
```
POST   /api/seeds                  → upload seed doc, trigger ingestion pipeline
GET    /api/seeds/{id}/status      → ingestion progress
```

**Reports**
```
GET    /api/reports/{simulationId}      → get generated report
POST   /api/reports/{simulationId}/chat → follow-up question to ReportAgent
```

**Internal (called by Python ingestion service)**
```
POST   /internal/graph/bulk        → bulk write to KuzuDB
```

### SignalR hub — SimulationHub
```csharp
public class SimulationHub : Hub
{
    // Client joins a group per simulationId
    public async Task Subscribe(string simulationId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, simulationId);

    // Server pushes progress via this method
    // Frontend receives: hub.on("ProgressUpdate", (progress) => ...)
}
```

Wire up: when `ISimulationRunner.WatchAsync` emits a `SimulationProgress`,
broadcast to the relevant SignalR group:
```csharp
await hubContext.Clients.Group(simulationId.ToString())
    .SendAsync("ProgressUpdate", progress);
```

### Middleware stack (in order)
1. Exception handling middleware → structured error responses
2. Request logging (Serilog structured)
3. Rate limiting (fixed window, 60 req/min per IP for external endpoints)
4. CORS (allow localhost:3000 in dev, configurable in prod)
5. Authentication (skip for now — add JWT placeholder)

### OpenAPI setup
Use Scalar for the OpenAPI UI (better than Swagger UI):
```csharp
app.MapScalarApiReference("/scalar");
app.MapOpenApi();
```
All endpoints must have `WithName`, `WithSummary`, `WithDescription`, and `Produces<T>` annotations.

## Tests required
**Report.Agent:**
- ReportAgentService: mock all plugins, verify report generated for a known event set
- SK plugins: unit test each plugin function with mock data
- Interactive chat: verify history is maintained across 3 turns

**Api.Gateway:**
- All REST endpoints: verify request/response contracts (WebApplicationFactory)
- SignalR: verify progress broadcast reaches subscribed clients
- Rate limiter: verify 61st request is rejected
- Internal /graph/bulk: verify it calls KuzuGraphStore.BulkImportAsync

## Done criteria
- [ ] All REST endpoints return correct status codes and response shapes
- [ ] SignalR broadcasts simulation progress in real-time
- [ ] ReportAgent generates a structured JSON report using SK tools
- [ ] Interactive ReportAgent chat maintains context across turns
- [ ] OpenAPI/Scalar UI available at /scalar with all endpoints documented
- [ ] Rate limiter operational on external endpoints
