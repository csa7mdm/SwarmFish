"use client"

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { api } from '@/lib/api';
import { SimulationSummary } from '@/types/api';
import { SimulationCard } from '@/components/SimulationCard';
import { Button } from '@/components/ui/button';
import { PlusIcon, ActivityIcon, RefreshCwIcon, Loader2Icon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

export default function DashboardPage() {
  const [simulations, setSimulations] = useState<SimulationSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchSimulations = async () => {
    try {
      setIsLoading(true);
      const data = await api.simulations.list();
      setSimulations(data);
    } catch (err) {
      setError('Failed to load simulations');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchSimulations();
    
    // SWR polling equivalent for mock
    const interval = setInterval(fetchSimulations, 10000);
    return () => clearInterval(interval);
  }, []);

  const activeCount = simulations.filter(s => s.status === 'Running' || s.status === 'Initialising').length;

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200">
      <header className="border-b border-slate-800 bg-slate-900/50 sticky top-0 z-10 backdrop-blur-sm">
        <div className="max-w-6xl mx-auto px-6 py-4 flex justify-between items-center">
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-md bg-blue-600 flex items-center justify-center shadow-lg shadow-blue-900/20">
              <ActivityIcon className="w-5 h-5 text-white" />
            </div>
            <h1 className="text-xl font-semibold tracking-tight text-white">SwarmFish</h1>
          </div>
          
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2 text-sm text-slate-400">
              <div className="w-2 h-2 rounded-full bg-green-500 animate-pulse" />
              System Online
            </div>
            <Link href="/simulations/new">
              <Button size="sm" className="bg-blue-600 hover:bg-blue-700 text-white border-0">
                <PlusIcon className="w-4 h-4 mr-1.5" />
                New Simulation
              </Button>
            </Link>
          </div>
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex justify-between items-end mb-6">
          <div>
            <h2 className="text-2xl font-semibold text-white mb-1">Recent Simulations</h2>
            <p className="text-slate-400 text-sm">
              Manage and monitor your swarm intelligence runs.
            </p>
          </div>
          
          <div className="flex items-center gap-3">
            <Badge variant="outline" className="bg-slate-900 border-slate-700 text-slate-300">
              {activeCount} Active
            </Badge>
            <Button 
              variant="outline" 
              size="icon" 
              className="bg-slate-900 border-slate-800 text-slate-400 hover:text-white"
              onClick={fetchSimulations}
              disabled={isLoading}
            >
              {isLoading ? <Loader2Icon className="w-4 h-4 animate-spin" /> : <RefreshCwIcon className="w-4 h-4" />}
            </Button>
          </div>
        </div>

        {error && (
          <div className="p-4 rounded-lg bg-red-500/10 border border-red-500/20 text-red-400 mb-6">
            {error}
          </div>
        )}

        {isLoading && simulations.length === 0 ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {[1, 2, 3].map(i => (
              <div key={i} className="h-48 rounded-xl bg-slate-900/50 border border-slate-800 animate-pulse" />
            ))}
          </div>
        ) : simulations.length === 0 ? (
          <div className="py-16 text-center border-2 border-dashed border-slate-800 rounded-xl bg-slate-900/20">
            <ActivityIcon className="w-12 h-12 text-slate-600 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-slate-300 mb-2">No simulations found</h3>
            <p className="text-slate-500 mb-6 max-w-md mx-auto">
              You haven't run any swarm intelligence simulations yet. Upload a seed document to get started.
            </p>
            <Link href="/simulations/new">
              <Button className="bg-white text-slate-950 hover:bg-slate-200">
                Create First Simulation
              </Button>
            </Link>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {simulations.map(sim => (
              <Link key={sim.id} href={`/simulations/${sim.id}`} className="block transition-transform hover:-translate-y-1">
                <SimulationCard simulation={sim} />
              </Link>
            ))}
          </div>
        )}
      </main>
    </div>
  );
}
