import { render, screen } from '@testing-library/react';
import { SimulationCard } from '@/components/SimulationCard';
import { SimulationSummary } from '@/types/api';

const baseSimulation: SimulationSummary = {
  id: 'sim-12345',
  seedDocumentTitle: 'Test Seed Document',
  agentCount: 1500,
  currentRound: 25,
  totalRounds: 100,
  createdAt: '2026-03-24T12:00:00Z',
  status: 'Running'
};

describe('SimulationCard', () => {
  it('renders all status states correctly', () => {
    const statuses = ['Initialising', 'Running', 'Paused', 'Completed', 'Failed'] as const;
    
    statuses.forEach(status => {
      const { unmount, container } = render(
        <SimulationCard simulation={{ ...baseSimulation, status }} />
      );
      
      screen.debug(container);
      console.log('Test HTML:', container.innerHTML);
      
      const badge = screen.getByText(status);
      expect(badge).toBeInTheDocument();
      
      unmount();
    });
  });

  it('renders simulation details properly', () => {
    render(<SimulationCard simulation={baseSimulation} />);
    
    expect(screen.getByText('Test Seed Document')).toBeInTheDocument();
    expect(screen.getByText('1,500 Agents', { exact: false })).toBeInTheDocument();
    expect(screen.getByText('25 / 100')).toBeInTheDocument();
    expect(screen.getByText('sim-1234', { exact: false })).toBeInTheDocument();
  });
});
