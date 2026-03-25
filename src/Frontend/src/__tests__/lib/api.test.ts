import { api } from '@/lib/api';

describe('API Client Stubs', () => {
  it('returns correctly mocked simulation lists', async () => {
    const list = await api.simulations.list();
    expect(Array.isArray(list)).toBe(true);
    expect(list.length).toBe(5); // We mock 5 initial simulations
    expect(list[0].status).toBe('Running');
  });

  it('returns a mocked prediction report', async () => {
    const report = await api.reports.get('sim-test');
    expect(report.simulationId).toBe('sim-test');
    expect(report.confidenceScore).toBeDefined();
    expect(report.findings.length).toBeGreaterThan(0);
  });

  it('generates a mock chat response', async () => {
    const response = await api.reports.chat('sim-test', 'Hello?', []);
    expect(typeof response).toBe('string');
    expect(response).toContain('Mock Agent Response');
  });
});
