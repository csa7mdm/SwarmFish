# SwarmFish

> A .NET-native, enterprise-grade swarm intelligence simulation engine.  
> Fork of the MiroFish concept, rebuilt for production .NET shops.

## What it does
Feed it a seed document (news article, policy draft, financial report, or novel).  
It extracts entities, generates agent personas, runs a parallel multi-agent social simulation,  
and produces a structured prediction report with an interactive follow-up interface.

## Architecture

| Component | Stack | Role |
|---|---|---|
| Agent orchestration | .NET 9 + Orleans 8 | Virtual actor model for thousands of parallel agents |
| Knowledge graph | KuzuDB (Rust, via P/Invoke) | Entity graph + GraphRAG retrieval |
| Agent memory | Zep Cloud (.NET SDK) | Persistent temporal memory per agent |
| Seed ingestion | Python 3.12 + FastAPI | NLP pipeline: entities → personas → seeded memories |
| LLM integration | Semantic Kernel 1.x | Model-agnostic, streaming, tool-use |
| API | ASP.NET Core 9 minimal API | REST + SignalR real-time |
| Frontend | Next.js 15 + TypeScript | Simulation control + report UI |

## Quickstart

```bash
# 1. Clone
git clone https://github.com/csa7mdm/SwarmFish.git
cd SwarmFish

# 2. Configure
cp .env.example .env.local
# Edit .env.local — add LLM_API_KEY, ZEP_API_KEY

# 3. Run
docker compose up -d

# Frontend → http://localhost:3000
# API      → http://localhost:5001
# API docs → http://localhost:5001/scalar
```

## Development setup

### Prerequisites
- .NET 9 SDK
- Node.js 22 + pnpm 9
- Python 3.12 + uv
- Docker Desktop

### Run each service locally
```bash
# .NET solution
dotnet run --project src/Api.Gateway

# Python ingestion
cd src/Pipeline.Ingestion && uv run uvicorn app.main:app --reload

# Frontend
cd src/Frontend && pnpm dev
```

### Run tests
```bash
dotnet test SwarmFish.sln
cd src/Pipeline.Ingestion && uv run pytest
cd src/Frontend && pnpm test
```

## Implementation status

| Component | Agent | Status |
|---|---|---|
| Core.Contracts + Domain | Agent A | 🔲 Not started |
| Orleans + Simulation Engine | Agent B | 🔲 Not started |
| KuzuDB Graph layer | Agent C | 🔲 Not started |
| Zep Memory + Ingestion | Agent D | 🔲 Not started |
| Report Agent + API Gateway | Agent E | 🔲 Not started |
| Frontend | Agent F | 🔲 Not started |

## Contributing / Agent workflow
See `AGENTS.md` for the full agent context file.  
See `docs/specs/` for the per-component implementation specs.

## Credits
Inspired by [MiroFish](https://github.com/666ghj/MiroFish) (OASIS / CAMEL-AI).  
Orleans simulation engine pattern based on Microsoft Orleans documentation.
