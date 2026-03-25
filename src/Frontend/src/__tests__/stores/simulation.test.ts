import { useSimulationStore } from '@/stores/simulation';
import { AgentEvent } from '@/types/api';

describe('useSimulationStore', () => {
  beforeEach(() => {
    // Reset store state before each test
    useSimulationStore.getState().reset();
  });

  it('initializes with empty state', () => {
    const state = useSimulationStore.getState();
    expect(state.currentSimulation).toBeNull();
    expect(state.events).toEqual([]);
    expect(state.progress).toBeNull();
  });

  it('appends events up to max limit (500)', () => {
    const store = useSimulationStore.getState();
    
    // Add 505 events
    for (let i = 0; i < 505; i++) {
      const event: AgentEvent = {
        agentId: `ag-${i}`,
        eventType: 'spoke',
        payload: `Test payload ${i}`,
        timestamp: new Date().toISOString()
      };
      
      // Need to re-fetch state as it creates new instances
      useSimulationStore.getState().appendEvent(event);
    }
    
    const finalState = useSimulationStore.getState();
    
    expect(finalState.events.length).toBe(500);
    // the first 5 should be dropped, so it starts from ag-5
    expect(finalState.events[0].agentId).toBe('ag-5');
    expect(finalState.events[499].agentId).toBe('ag-504');
  });

  it('updates currentSimulation correctly', () => {
    const mockSim = {
      id: 'sim-test',
      status: 'Running' as const,
      seedDocumentTitle: 'Test',
      agentCount: 10,
      currentRound: 0,
      totalRounds: 100,
      createdAt: new Date().toISOString(),
      predictionQuery: 'Test',
      mode: 'Standard' as const,
      events: []
    };
    
    useSimulationStore.getState().setCurrentSimulation(mockSim);
    expect(useSimulationStore.getState().currentSimulation).toEqual(mockSim);
  });
});
