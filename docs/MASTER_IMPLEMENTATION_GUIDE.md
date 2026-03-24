# SwarmFish — Master Implementation Guide
## For Antigravity IDE + Claude Opus 4.6 — Parallel Agent Execution

---

## Overview

This guide coordinates 6 parallel agents implementing SwarmFish from scratch.
Each agent has a dedicated spec file in `docs/specs/`. This document defines:
- The dependency order (which agents block which)
- The parallel execution map
- The shared context all agents must keep in mind
- The integration checkpoints between phases

---

## Dependency Graph

```
Phase 0 (Agent A only — blocks everyone):
  Agent A: Core.Contracts ──────────────────────────────────────────────────┐
                                                                             │
Phase 1 (parallel, start after Core.Contracts merged):                      │
  Agent A: Core.Domain ─────────────────────────────────────────────────┐   │
  Agent B: Agents.Orleans ──────────────────────────────────────────┐   │   │
  Agent C: Graph.KuzuDB ────────────────────────────────────────┐   │   │   │
  Agent D: Memory.Zep ─────────────────────────────────────┐    │   │   │   │
  Agent F: Frontend scaffold ──────────────────────────┐   │    │   │   │   │
                                                        │   │    │   │   │   │
Phase 2 (parallel, after Phase 1 components done):      │   │    │   │   │   │
  Agent B: Simulation.Engine ────────────────────── (needs B+C+D)  │   │   │
  Agent D: Pipeline.Ingestion ─────────────────── (needs A+C+D)    │   │   │
  Agent E: Report.Agent + Api.Gateway ───────── (needs all above)  │   │   │
  Agent F: Frontend wiring ────────────────── (needs E)            │   │   │
```

---

## Antigravity Manager View — Agent Assignment

Open Antigravity Manager View. Create 6 agents. Assign exactly as follows:

### Agent A — Foundation
**Model**: Claude Opus 4.6 (Thinking)
**Working directory**: `src/Core.Contracts` then `src/Core.Domain`
**Spec file**: `docs/specs/SPEC_AGENT_A_CONTRACTS_DOMAIN.md`
**Prompt to start**:
```
Read AGENTS.md at the repo root, then read docs/specs/SPEC_AGENT_A_CONTRACTS_DOMAIN.md.
Implement Phase 1 (Core.Contracts) completely. Do not proceed to Phase 2 until
all interfaces in Core.Contracts compile with zero warnings and unit tests pass.
Commit with: feat(contracts): add core interfaces and contracts
Then notify that Phase 1 is complete so other agents can unblock.
```

### Agent B — Orchestration
**Model**: Claude Opus 4.6 (Thinking)
**Working directory**: `src/Agents.Orleans` then `src/Simulation.Engine`
**Spec file**: `docs/specs/SPEC_AGENT_B_ORLEANS_ENGINE.md`
**Prompt to start**:
```
Read AGENTS.md at the repo root, then read docs/specs/SPEC_AGENT_B_ORLEANS_ENGINE.md.
Wait for Agent A's feat(contracts) commit before starting Phase 1.
Begin with project setup and IAgentGrain definition. Do not implement ProcessTickAsync
until Memory.Zep and Graph.KuzuDB project stubs exist (Agent C and D Phase 1).
Use mock implementations of IMemoryStore and IGraphStore in the interim.
```

### Agent C — Graph
**Model**: Claude Opus 4.6 (Thinking)
**Working directory**: `src/Graph.KuzuDB`
**Spec file**: `docs/specs/SPEC_AGENT_C_GRAPH_KUZU.md`
**Prompt to start**:
```
Read AGENTS.md at the repo root, then read docs/specs/SPEC_AGENT_C_GRAPH_KUZU.md.
Wait for Agent A's feat(contracts) commit. Then start immediately with Phase 1
(KuzuDB native bindings). Download KuzuDB 0.7+ C API headers from
https://github.com/kuzudb/kuzu/releases — verify the P/Invoke signatures against
the actual C API before implementing.
```

### Agent D — Memory + Ingestion
**Model**: Claude Sonnet 4.6 (faster for Python work)
**Working directory**: `src/Memory.Zep` then `src/Pipeline.Ingestion`
**Spec file**: `docs/specs/SPEC_AGENT_D_MEMORY_INGESTION.md`
**Prompt to start**:
```
Read AGENTS.md at the repo root, then read docs/specs/SPEC_AGENT_D_MEMORY_INGESTION.md.
Start Memory.Zep immediately after Agent A's contracts are merged.
For Pipeline.Ingestion, start scaffolding the FastAPI project structure in parallel
but do not implement graph_writer.py until Agent C's KuzuDB bulk import endpoint
is available. Use a mock HTTP client in the interim.
```

### Agent E — Report + API
**Model**: Claude Opus 4.6 (Thinking)
**Working directory**: `src/Report.Agent` then `src/Api.Gateway`
**Spec file**: `docs/specs/SPEC_AGENT_E_REPORT_API.md`
**Prompt to start**:
```
Read AGENTS.md at the repo root, then read docs/specs/SPEC_AGENT_E_REPORT_API.md.
Scaffold both projects immediately. Implement Api.Gateway endpoints with stub
implementations returning mock data first — this unblocks Agent F.
Wire up real implementations once Agents B, C, D are merged.
Commit the stub gateway early with: feat(api): scaffold gateway with stub endpoints
```

### Agent F — Frontend
**Model**: Claude Sonnet 4.6 (UI work)
**Working directory**: `src/Frontend`
**Spec file**: `docs/specs/SPEC_AGENT_F_FRONTEND.md`
**Prompt to start**:
```
Read AGENTS.md at the repo root, then read docs/specs/SPEC_AGENT_F_FRONTEND.md.
Start immediately with project scaffold, component library setup, and all
UI components. Use mock/hardcoded data for all API calls initially.
Wire up to real API once Agent E commits the stub gateway.
Build the SimulationCard, AgentEventFeed, and launch wizard first.
```

---

## Shared Context — Every Agent Must Know This

### What we're building
A production-grade .NET rewrite of MiroFish — a multi-agent social simulation engine
for prediction. The core insight is: Python GIL + lack of clean architecture in MiroFish
makes it hard to scale and impossible to deploy in enterprise .NET shops.
SwarmFish solves both by using Orleans as the agent model and KuzuDB as the graph store.

### The user flow end-to-end
```
User uploads PDF → Python ingestion runs NLP → Entities written to KuzuDB
→ Personas generated → Zep memories seeded
→ User launches simulation → Orleans grains spawn (one per agent)
→ Tick loop runs N rounds → each agent: reads memory + graph → calls LLM → emits event
→ Events stream to frontend via SignalR
→ Simulation completes → ReportAgent synthesises → user reads report + chats
```

### Non-negotiable constraints every agent must enforce
1. `Core.Contracts` has zero NuGet dependencies — ever.
2. No `async void` methods anywhere.
3. All `CancellationToken` parameters must actually be passed through, not ignored.
4. Every LLM call must have a timeout (default 30 seconds) and retry (max 3, exponential backoff).
5. No sensitive values in logs — mask API keys, user content.
6. All `IDisposable`/`IAsyncDisposable` resources wrapped in `using` or DI-managed lifetimes.

### Integration test checkpoint
Before any agent declares their component "done", run the integration test:
```bash
docker compose up -d
# Then run the smoke test (src/Tests.Integration/SmokeTest.cs):
dotnet test tests/integration --filter "Category=Smoke"
```
The smoke test: uploads a 500-word text file, triggers ingestion, starts a 10-agent
5-round simulation, waits for completion, retrieves the report.

---

## Git workflow

```
main         ← production-ready, protected
develop      ← integration branch, all PRs target here
feat/agent-a ← Agent A's branch
feat/agent-b ← Agent B's branch
feat/agent-c ← Agent C's branch
feat/agent-d ← Agent D's branch
feat/agent-e ← Agent E's branch
feat/agent-f ← Agent F's branch
```

Each agent works on their feature branch. PRs → develop.
When all 6 PRs are merged to develop and CI is green, merge develop → main.

---

## Phase completion checklist

### Phase 0 complete when:
- [ ] `src/Core.Contracts` compiles with zero warnings
- [ ] All 6 interfaces defined as per spec
- [ ] Unit tests pass for contract implementations

### Phase 1 complete when:
- [ ] Agent B: AgentGrain activates, processes 1 tick with mocks
- [ ] Agent C: IGraphStore implemented, integration test vs real KuzuDB passes
- [ ] Agent D: IMemoryStore implemented, integration test vs real Zep passes
- [ ] Agent E: All REST endpoints return 200 with mock data
- [ ] Agent F: All pages render with mock data, no TypeScript errors

### Phase 2 complete when:
- [ ] Integration smoke test passes (see above)
- [ ] SignalR broadcasts simulation progress to frontend
- [ ] ReportAgent generates a structured report for the smoke test simulation
- [ ] Frontend displays live events from a real simulation

### Done (v1.0) when:
- [ ] All phase 2 criteria met
- [ ] CI green on main
- [ ] README updated with accurate quickstart
- [ ] Docker Compose starts the full stack with `docker compose up -d`

---

## Known risks + mitigations

| Risk | Mitigation |
|---|---|
| KuzuDB P/Invoke signatures differ from docs | Agent C: verify against actual .h files from release |
| Zep free tier rate limits under simulation load | Agent D: sliding window rate limiter with backpressure |
| Orleans grain fanout causes memory pressure | Agent B: batch size configurable, default 50 per wave |
| LLM response not valid JSON | Agent B: retry with explicit JSON reminder in re-prompt |
| Herd bias makes all agents agree | Agent B: HerdBiasCorrector suppresses 20% when diversity < 0.3 |
| Antigravity context window limits | Each agent spec is self-contained; no cross-spec reading needed |
