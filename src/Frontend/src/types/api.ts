export type AgentStatus = 'Idle' | 'Active' | 'Suppressed' | 'Terminated';

export interface AgentEvent {
  agentId: string;
  eventType: 'spoke' | 'reacted' | 'silent' | 'moved' | string;
  payload: string;
  timestamp: string;
}

export type SimulationMode = 'Standard' | 'HighFidelity' | 'FastSweep';

export interface SimulationConfig {
  seedDocumentId: string;
  agentCount: number;
  maxRounds: number;
  predictionQuery: string;
  mode: SimulationMode;
}

export type SimulationState = 'Initialising' | 'Running' | 'Paused' | 'Completed' | 'Failed';

export interface SimulationProgress {
  simulationId: string;
  currentRound: number;
  totalRounds: number;
  state: SimulationState;
  timestamp: string;
}

export interface SimulationSummary {
  id: string;
  status: SimulationState;
  seedDocumentTitle: string;
  agentCount: number;
  currentRound: number;
  totalRounds: number;
  createdAt: string;
}

export interface SimulationDetail extends SimulationSummary {
  predictionQuery: string;
  mode: SimulationMode;
  events: AgentEvent[];
}

export interface PredictionFinding {
  category: string;
  description: string;
  confidence: number;
  supportingAgentIds: string[];
}

export interface TimelineEvent {
  simulatedAt: string;
  description: string;
  involvedAgents: string[];
}

export interface PredictionReport {
  simulationId: string;
  summary: string;
  findings: PredictionFinding[];
  timeline: TimelineEvent[];
  confidenceScore: number;
}

export type SeedDocumentType = 'NewsArticle' | 'PolicyDocument' | 'FinancialReport' | 'Novel' | 'Custom';

export interface SeedDocument {
  id: string;
  title: string;
  rawContent: string;
  type: SeedDocumentType;
  metadata: Record<string, string>;
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
}
