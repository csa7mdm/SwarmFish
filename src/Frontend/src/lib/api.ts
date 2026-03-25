import { 
  SimulationSummary, 
  SimulationConfig, 
  SimulationDetail, 
  PredictionReport, 
  ChatMessage 
} from '@/types/api';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5001';

async function fetchApi<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!res.ok) {
    let message = 'An error occurred';
    try {
      const errData = await res.json();
      message = errData.message || res.statusText;
    } catch {
      message = res.statusText;
    }
    throw new Error(message);
  }

  // Some endpoints might return empty (204)
  if (res.status === 204) return {} as T;
  return res.json();
}

export const api = {
  simulations: {
    list: (): Promise<SimulationSummary[]> => 
      fetchApi<SimulationSummary[]>('/api/simulations'),
    
    get: (id: string): Promise<SimulationDetail> => 
      fetchApi<SimulationDetail>(`/api/simulations/${id}`),
    
    start: (config: SimulationConfig): Promise<{ simulationId: string }> => 
      fetchApi<{ simulationId: string }>('/api/simulations', {
        method: 'POST',
        body: JSON.stringify(config),
      }),
    
    pause: (id: string) => 
      fetchApi(`/api/simulations/${id}/pause`, { method: 'POST' }),
    
    resume: (id: string) => 
      fetchApi(`/api/simulations/${id}/resume`, { method: 'POST' }),
    
    stop: (id: string) => 
      fetchApi(`/api/simulations/${id}/stop`, { method: 'POST' })
  },
  
  seeds: {
    upload: async (file: File, query?: string) => {
      const formData = new FormData();
      formData.append('file', file);
      if (query) {
        formData.append('query', query);
      }

      const res = await fetch(`${API_URL}/api/seeds`, {
        method: 'POST',
        body: formData,
      });

      if (!res.ok) {
        throw new Error('Upload failed');
      }

      return res.json();
    }
  },
  
  reports: {
    get: (simulationId: string): Promise<PredictionReport> => 
      fetchApi<PredictionReport>(`/api/reports/${simulationId}`),
      
    // The chat endpoint uses streaming, so we fetch and process the stream manually in components,
    // or expose a generator here. Let's expose an async function that returns the Response.
    // The implementation specifically asked to stream the response. 
    chat: async (simulationId: string, message: string, history: ChatMessage[]): Promise<Response> => {
      return fetch(`${API_URL}/api/reports/${simulationId}/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message, history })
      });
    }
  }
};
