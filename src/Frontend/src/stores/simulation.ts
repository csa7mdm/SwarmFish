import { create } from 'zustand';
import { AgentEvent, SimulationProgress, SimulationDetail } from '@/types/api';

interface SimulationStore {
  currentSimulation: SimulationDetail | null;
  events: AgentEvent[];
  progress: SimulationProgress | null;
  setCurrentSimulation: (sim: SimulationDetail | null) => void;
  handleProgress: (p: SimulationProgress) => void;
  appendEvent: (e: AgentEvent) => void;
  reset: () => void;
}

const MAX_EVENTS = 500;

export const useSimulationStore = create<SimulationStore>((set) => ({
  currentSimulation: null,
  events: [],
  progress: null,
  
  setCurrentSimulation: (sim) => set({ currentSimulation: sim }),
  
  handleProgress: (p) => set({ progress: p }),
  
  appendEvent: (e) => set((state) => {
    const newEvents = [...state.events, e];
    if (newEvents.length > MAX_EVENTS) {
      newEvents.shift(); // Drop the oldest event to maintain 500 limit
    }
    return { events: newEvents };
  }),
  
  reset: () => set({ 
    currentSimulation: null, 
    events: [], 
    progress: null 
  }),
}));
