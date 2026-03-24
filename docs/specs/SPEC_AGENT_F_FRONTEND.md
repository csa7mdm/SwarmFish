# Spec: Agent F — Frontend
> Read root AGENTS.md first. Can start scaffolding in parallel with all other agents.
> Wire up to real API only once Agent E (Api.Gateway) is done.

## Your role
Build the **simulation control interface**: the god's-eye view dashboard where users
upload seed documents, configure and launch simulations, watch agents interact in
real-time, and interrogate the ReportAgent.

## Tech stack
- Next.js 15 (App Router, Server Components where appropriate)
- TypeScript (strict mode)
- Tailwind CSS + shadcn/ui components
- Zustand for client state
- SWR for data fetching
- socket.io-client or native WebSocket for SignalR

## Project setup
```bash
cd src/Frontend
pnpm create next-app@latest . --typescript --tailwind --app --src-dir --import-alias "@/*"
pnpm add @microsoft/signalr zustand swr lucide-react
pnpm dlx shadcn@latest init
```

## Pages and routes

### `/` — Dashboard
- Recent simulations list (SWR polling `/api/simulations`)
- "New Simulation" CTA
- System status (connected / disconnected indicator)

### `/simulations/new` — Launch wizard
3-step wizard:
1. **Upload seed** — drag-drop zone, accepts PDF/TXT/MD, shows ingestion progress
2. **Configure** — sliders for agent count (10–10,000), max rounds (10–500), prediction query text area
3. **Review + launch** — summary card, "Launch Simulation" button

### `/simulations/[id]` — Live simulation view
The most important page. Layout:
```
┌─────────────────────────────────────────────────┐
│  Header: Simulation ID | Status | Round X/Y      │
│  Progress bar                                    │
├────────────────────┬────────────────────────────┤
│  Agent Event Feed  │  Simulation Stats          │
│  (scrolling list   │  - Active agents           │
│   of AgentEvents   │  - Event types breakdown   │
│   as they come in) │  - Diversity score         │
│                    │  - Avg response time       │
├────────────────────┴────────────────────────────┤
│  Controls: [Pause] [Resume] [Stop]              │
└─────────────────────────────────────────────────┘
```

SignalR connection:
```typescript
const connection = new HubConnectionBuilder()
  .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/hubs/simulation`)
  .withAutomaticReconnect()
  .build();

await connection.start();
await connection.invoke("Subscribe", simulationId);

connection.on("ProgressUpdate", (progress: SimulationProgress) => {
  useSimulationStore.getState().handleProgress(progress);
});
```

### `/simulations/[id]/report` — Prediction report
Two panels:
- **Left**: Structured report display (Summary, Findings with confidence bars, Timeline, Dissenting views)
- **Right**: Chat interface for follow-up questions to ReportAgent

Chat interface:
```typescript
// POST /api/reports/{simulationId}/chat
const sendMessage = async (message: string) => {
  const response = await fetch(`/api/reports/${simulationId}/chat`, {
    method: 'POST',
    body: JSON.stringify({ message, history: chatHistory })
  });
  // Stream response with ReadableStream
};
```

## Components to build

### `<SimulationCard>` — used in dashboard list
Props: `simulation: SimulationSummary`
Shows: status badge, seed document title, agent count, round progress, created time

### `<AgentEventFeed>` — real-time scrolling event list
Props: `simulationId: string`
- Auto-scrolls to newest event
- Each event row: agent icon + name, event type badge, payload excerpt, timestamp
- Color code by event type: spoke=blue, reacted=amber, silent=gray, moved=teal

### `<ConfidenceBar>` — for report findings
Props: `score: number (0-1), label: string`
Renders a progress bar with color gradient: red (0) → amber (0.5) → green (1)

### `<SeedUploader>` — drag-drop file zone
Uses File API, shows upload progress, then ingestion pipeline progress via polling

### `<SimulationConfig>` — step 2 of wizard
All sliders + inputs. Validates: agentCount min 10, max 10000. Rounds min 10, max 500.

## State management (Zustand)

```typescript
// stores/simulation.ts
interface SimulationStore {
  currentSimulation: SimulationDetail | null;
  events: AgentEvent[];
  progress: SimulationProgress | null;
  handleProgress: (p: SimulationProgress) => void;
  appendEvent: (e: AgentEvent) => void;
  reset: () => void;
}
```

Cap `events` array at 500 entries (drop oldest) to avoid memory growth on long simulations.

## Type definitions (mirror Core.Contracts)
Create `src/types/api.ts` with TypeScript equivalents of all DTOs from Core.Contracts.
These must stay in sync manually — document this clearly.

## API client layer
Create `src/lib/api.ts` — a typed fetch wrapper:
```typescript
export const api = {
  simulations: {
    start: (config: SimulationConfig) => post<{simulationId: string}>('/api/simulations', config),
    get: (id: string) => get<SimulationDetail>(`/api/simulations/${id}`),
    pause: (id: string) => post(`/api/simulations/${id}/pause`),
    // ...
  },
  seeds: {
    upload: (file: File, query: string) => { /* multipart/form-data */ },
  },
  reports: {
    get: (simulationId: string) => get<PredictionReport>(`/api/reports/${simulationId}`),
    chat: (simulationId: string, message: string, history: ChatMessage[]) =>
      post<string>(`/api/reports/${simulationId}/chat`, { message, history }),
  }
};
```

## Environment
```
NEXT_PUBLIC_API_URL=http://localhost:5001
```

## Tests required
- `<SimulationCard>`: renders correctly for all status states
- `<AgentEventFeed>`: caps at 500 events, auto-scrolls
- `<ConfidenceBar>`: correct color at 0, 0.5, 1.0
- `SimulationStore`: progress updates propagate, events append correctly
- API client: mock fetch, verify correct URLs and payloads

## Done criteria
- [ ] All 4 pages render without errors
- [ ] SignalR connects and displays live events during a real simulation
- [ ] Launch wizard completes a full flow (upload → configure → launch)
- [ ] Report page shows structured findings + working chat
- [ ] Mobile-responsive (works at 375px width)
- [ ] TypeScript strict mode — zero type errors
