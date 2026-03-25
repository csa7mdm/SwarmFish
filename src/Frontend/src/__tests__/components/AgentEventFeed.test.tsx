import { render, screen } from '@testing-library/react';
import { AgentEventFeed } from '@/components/AgentEventFeed';
import { useSimulationStore } from '@/stores/simulation';
import { AgentEvent } from '@/types/api';

// Partially mock the store wrapper
jest.mock('@/stores/simulation', () => ({
  useSimulationStore: jest.fn()
}));

const mockUseSimulationStore = useSimulationStore as unknown as jest.Mock;

describe('AgentEventFeed', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders empty state when no events exist', () => {
    mockUseSimulationStore.mockReturnValue([]);
    
    render(<AgentEventFeed simulationId="sim-12345" />);
    
    expect(screen.getByText(/Waiting for events from simulation sim-1234/)).toBeInTheDocument();
  });

  it('renders events correctly', () => {
    const mockEvents: AgentEvent[] = [
      { agentId: 'ag-001', eventType: 'spoke', payload: 'Hello world', timestamp: '2026-03-24T12:00:00Z' },
      { agentId: 'ag-002', eventType: 'reacted', payload: 'Nice to meet you', timestamp: '2026-03-24T12:00:01Z' }
    ];
    
    // Simulate what the component receives from the store selector
    mockUseSimulationStore.mockImplementation((selector) => {
      // The component calls state => state.events
      return mockEvents;
    });
    
    render(<AgentEventFeed simulationId="sim-12345" />);
    
    expect(screen.getByText('Hello world')).toBeInTheDocument();
    expect(screen.getByText('Nice to meet you')).toBeInTheDocument();
  });
});
