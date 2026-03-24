# SwarmFish — AGENTS.md
> AI-native context file. Read this before touching any file.

## What this project is
SwarmFish is a .NET-native, polyglot swarm intelligence simulation engine inspired by MiroFish.
It ingests real-world "seed" documents, builds a knowledge graph, spins up thousands of agents
with persistent memory and independent personas, runs parallel social simulation, and generates
prediction reports. Think MiroFish but enterprise-grade, clean architecture, and built to run
in production .NET shops.

## Monorepo layout
```
src/
  Core.Contracts/        # Shared interfaces + DTOs — NO implementations here
  Core.Domain/           # Domain entities, value objects, aggregates
  Agents.Orleans/        # Orleans grain definitions + activation logic (.NET 9)
  Graph.KuzuDB/          # KuzuDB wrapper — Rust FFI bindings exposed via C API
  Memory.Zep/            # Agent memory abstraction over Zep Cloud
  Pipeline.Ingestion/    # Python microservice — seed doc → personas → graph
  Simulation.Engine/     # Orchestration loop, tick scheduler, event bus
  Report.Agent/          # ReportAgent with Semantic Kernel toolset
  Api.Gateway/           # ASP.NET Core 9 minimal API — REST + SignalR
  Frontend/              # Next.js 15 + TypeScript (App Router)
tests/
  unit/                  # xUnit for .NET, pytest for Python
  integration/           # Docker Compose based, real services
  e2e/                   # Playwright tests against the full stack
docs/specs/              # Per-component implementation specs (read before implementing)
.agents/skills/          # Agent skills for specialised tasks
```

## Hard rules — NEVER violate these
- `Core.Contracts` has ZERO external dependencies. Only .NET BCL.
- No circular dependencies between projects. Dependency direction: always inward toward Core.
- All public API surfaces must have XML doc comments.
- Every new class/interface requires a corresponding unit test file before the PR merges.
- Secrets go in `.env.local` (git-ignored). Never hardcode keys.
- All async methods must be truly async — no `.Result` or `.Wait()` anywhere.
- Git commits follow Conventional Commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`.

## Build commands
```bash
# .NET solution
dotnet build SwarmFish.sln
dotnet test SwarmFish.sln --no-build

# Python ingestion service
cd src/Pipeline.Ingestion
uv sync && uv run pytest

# Frontend
cd src/Frontend
pnpm install && pnpm dev

# Full stack (Docker)
docker compose up -d
```

## Technology decisions (settled — do not re-debate)
| Component              | Language / Framework        | Why                                   |
|------------------------|-----------------------------|---------------------------------------|
| Agent orchestration    | .NET 9 + Orleans 8          | Virtual actor model = perfect fit     |
| Graph database         | KuzuDB (Rust, C API)        | Embedded, zero-copy, GraphRAG native  |
| Agent memory           | Zep Cloud SDK (.NET)        | Persistent temporal memory per agent  |
| Seed ingestion/NLP     | Python 3.12 + uv            | NLP ecosystem lock-in (spaCy etc.)    |
| LLM calls              | Semantic Kernel 1.x (.NET)  | Streaming, tool-use, model-agnostic   |
| API gateway            | ASP.NET Core 9 minimal API  | Performance, SignalR for real-time    |
| Frontend               | Next.js 15 + TypeScript     | App Router, Server Components         |
| Containerisation       | Docker + docker-compose     | Dev parity, prod deployment path      |

## Shared interfaces (in Core.Contracts — read before implementing)
Key interfaces all agents must respect:
- `IAgent` — identity, persona, memory handle, tick handler
- `ISimulationTick` — event dispatched each simulation round
- `IGraphStore` — graph query/mutation contract
- `IMemoryStore` — agent memory CRUD contract
- `ISeedDocument` — input document model
- `IPredictionReport` — output report model

Full interface definitions are in `src/Core.Contracts/Interfaces/`.

## Environment variables required
```
LLM_API_KEY=           # Any OpenAI-compatible LLM
LLM_BASE_URL=          # e.g. https://api.anthropic.com/v1
LLM_MODEL_NAME=        # e.g. claude-sonnet-4-6

ZEP_API_KEY=           # https://app.getzep.com free tier is fine

KUZU_DB_PATH=          # Local path for embedded KuzuDB
ORLEANS_CLUSTER_ID=swarmfish
ORLEANS_SERVICE_ID=swarmfish-dev

NEXT_PUBLIC_API_URL=http://localhost:5001
```

## Component ownership map (parallel agent assignment)
Each agent works on exactly one directory. They share `Core.Contracts` as read-only.
- **Agent A** → `src/Core.Contracts` + `src/Core.Domain`
- **Agent B** → `src/Agents.Orleans` + `src/Simulation.Engine`
- **Agent C** → `src/Graph.KuzuDB`
- **Agent D** → `src/Memory.Zep` + `src/Pipeline.Ingestion`
- **Agent E** → `src/Report.Agent` + `src/Api.Gateway`
- **Agent F** → `src/Frontend`

See `docs/specs/` for the detailed spec each agent must read before starting.

## Integration contract between components
```
Pipeline.Ingestion  →  Graph.KuzuDB      (writes entity graph via IGraphStore)
Pipeline.Ingestion  →  Memory.Zep        (seeds initial agent memories)
Agents.Orleans      →  Memory.Zep        (reads/writes per-tick)
Agents.Orleans      →  Graph.KuzuDB      (reads persona + relationship context)
Simulation.Engine   →  Agents.Orleans    (dispatches ticks, collects events)
Simulation.Engine   →  Api.Gateway       (streams progress via SignalR)
Report.Agent        →  Graph.KuzuDB      (queries post-simulation state)
Report.Agent        →  Memory.Zep        (reads full agent history)
Api.Gateway         →  Simulation.Engine (starts/stops/configures runs)
Frontend            →  Api.Gateway       (REST + SignalR WebSocket)
```

## PR discipline
- One PR per component (match the agent ownership map above).
- PRs may only touch files within their assigned directory + tests/.
- Exception: PRs may READ Core.Contracts but must not modify it without explicit human approval.
- Each PR must include: implementation + unit tests + updated component AGENTS.md.
