import { 
  SimulationSummary, 
  SimulationConfig, 
  SimulationDetail, 
  PredictionReport, 
  ChatMessage 
} from '@/types/api';

// --- MOCK DATA ---

const mockSimulations: SimulationSummary[] = [
  {
    id: 'sim-1111-2222',
    status: 'Running',
    seedDocumentTitle: 'Q3 Earnings Report - Acme Corp',
    agentCount: 500,
    currentRound: 42,
    totalRounds: 100,
    createdAt: new Date().toISOString()
  },
  {
    id: 'sim-3333-4444',
    status: 'Completed',
    seedDocumentTitle: 'Federal Reserve Policy Statement H2 2026',
    agentCount: 1500,
    currentRound: 250,
    totalRounds: 250,
    createdAt: new Date(Date.now() - 86400000).toISOString()
  },
  {
    id: 'sim-5555-6666',
    status: 'Paused',
    seedDocumentTitle: 'Global Supply Chain Analysis',
    agentCount: 1000,
    currentRound: 75,
    totalRounds: 150,
    createdAt: new Date(Date.now() - 172800000).toISOString()
  },
  {
    id: 'sim-7777-8888',
    status: 'Initialising',
    seedDocumentTitle: 'Competitor Product Launch Leak',
    agentCount: 200,
    currentRound: 0,
    totalRounds: 50,
    createdAt: new Date(Date.now() - 600000).toISOString()
  },
  {
    id: 'sim-9999-0000',
    status: 'Failed',
    seedDocumentTitle: 'Invalid Data Source',
    agentCount: 10,
    currentRound: 5,
    totalRounds: 10,
    createdAt: new Date(Date.now() - 259200000).toISOString()
  }
];

const mockDetail: SimulationDetail = {
  ...mockSimulations[0],
  predictionQuery: 'Will Acme Corp beat market expectations over the next quarter?',
  mode: 'Standard',
  events: [] // Filled by socket in real app
};

const mockReport: PredictionReport = {
  simulationId: 'sim-3333-4444',
  summary: 'Based on the social simulation of 1,500 expert personas reviewing the latest Fed policy, the swarm predicts a high likelihood of a 25bps rate cut in the upcoming quarter. Agents with quantitative easing biases quickly achieved consensus, dragging neutral agents along. The remaining 15% holdouts highlight inflation risks in the services sector.',
  confidenceScore: 0.82,
  findings: [
    {
      category: 'Monetary Policy',
      description: 'Strong consensus forming around a 25bps dovish pivot.',
      confidence: 0.88,
      supportingAgentIds: ['ag-1', 'ag-2']
    },
    {
      category: 'Market Reaction',
      description: 'Tech valuations projected to expand by 5-8% post-announcement.',
      confidence: 0.76,
      supportingAgentIds: ['ag-3', 'ag-4']
    },
    {
      category: 'Dissenting view',
      description: 'Sticky services inflation may force a hawkish pause instead.',
      confidence: 0.35,
      supportingAgentIds: ['ag-5']
    }
  ],
  timeline: [
    {
      simulatedAt: new Date(Date.now() - 3600000).toISOString(),
      description: 'Macro-economist personas establish initial dovish thesis.',
      involvedAgents: ['ag-1', 'ag-5']
    },
    {
      simulatedAt: new Date(Date.now() - 1800000).toISOString(),
      description: 'Retail trading personas panic-buy theoretical equities, forcing price-discovery agents to adapt.',
      involvedAgents: ['ag-2', 'ag-3']
    }
  ]
};

// --- API STUB ---

const delay = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

export const api = {
  simulations: {
    list: async (): Promise<SimulationSummary[]> => {
      await delay(800);
      return mockSimulations;
    },
    
    get: async (id: string): Promise<SimulationDetail> => {
      await delay(500);
      const sim = mockSimulations.find(s => s.id === id);
      if (!sim) throw new Error('Simulation not found');
      return {
        ...sim,
        predictionQuery: 'What is the impact of X on Y?',
        mode: 'Standard',
        events: []
      };
    },
    
    start: async (config: SimulationConfig): Promise<{ simulationId: string }> => {
      await delay(1200);
      return { simulationId: `sim-new-${Date.now()}` };
    },
    
    pause: async (id: string) => {
      await delay(300);
      return { success: true };
    },
    
    resume: async (id: string) => {
      await delay(300);
      return { success: true };
    },
    
    stop: async (id: string) => {
      await delay(300);
      return { success: true };
    }
  },
  
  seeds: {
    upload: async (file: File, query?: string) => {
      await delay(2000);
      return { id: `seed-${Date.now()}`, title: file.name };
    }
  },
  
  reports: {
    get: async (simulationId: string): Promise<PredictionReport> => {
      await delay(1000);
      return { ...mockReport, simulationId };
    },
    
    chat: async (simulationId: string, message: string, history: ChatMessage[]): Promise<string> => {
      await delay(1500);
      return `[Mock Agent Response to "${message}"] Based on my analysis of the simulation data, the agents were primarily concerned with the secondary macro effects of the policy change. Would you like me to break down the specific agent clusters that formed around this idea?`;
    }
  }
};
