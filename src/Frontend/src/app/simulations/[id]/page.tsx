"use client"

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useSimulationStore } from '@/stores/simulation';
import { api } from '@/lib/api';
import { AgentEventFeed } from '@/components/AgentEventFeed';
import { Progress } from '@/components/ui/progress';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { PlayIcon, PauseIcon, SquareIcon, ArrowLeftIcon, FileTextIcon } from 'lucide-react';
import Link from 'next/link';

export default function LiveSimulationPage() {
  const params = useParams();
  const router = useRouter();
  const simulationId = params.id as string;
  
  const { currentSimulation, setCurrentSimulation, appendEvent, reset } = useSimulationStore();
  const [isLoading, setIsLoading] = useState(true);
  const [isError, setIsError] = useState(false);

  // Load simulator data
  useEffect(() => {
    reset();
    api.simulations.get(simulationId)
      .then(sim => {
        setCurrentSimulation(sim);
        setIsLoading(false);
      })
      .catch(() => {
        setIsError(true);
        setIsLoading(false);
      });
  }, [simulationId, setCurrentSimulation, reset]);

  // Mock SignalR Event stream
  useEffect(() => {
    if (!currentSimulation || currentSimulation.status !== 'Running') return;

    const eventTypes = ['spoke', 'reacted', 'silent', 'moved'];
    const mockPayloads = [
      "I believe we should hold our position given the latest data.",
      "Agreed, the downside risk is too high.",
      "Observed movement in the secondary market.",
      "Adjusting prior assumptions based on new peer consensus.",
      "No change to my current thesis.",
    ];

    const interval = setInterval(() => {
      // Create 1-3 random events
      const numEvents = Math.floor(Math.random() * 3) + 1;
      
      for(let i=0; i<numEvents; i++) {
        const type = eventTypes[Math.floor(Math.random() * eventTypes.length)];
        appendEvent({
          agentId: `ag-${Math.floor(Math.random() * 1000).toString().padStart(4, '0')}`,
          eventType: type,
          payload: mockPayloads[Math.floor(Math.random() * mockPayloads.length)],
          timestamp: new Date().toISOString()
        });
      }
      
      // Occasionally update progress mock
      if (Math.random() > 0.8) {
        setCurrentSimulation({
          ...currentSimulation,
          currentRound: Math.min(currentSimulation.currentRound + 1, currentSimulation.totalRounds)
        });
      }
      
    }, 800);

    return () => clearInterval(interval);
  }, [currentSimulation, appendEvent, setCurrentSimulation]);

  if (isLoading) return <div className="min-h-screen bg-slate-950 flex items-center justify-center text-slate-500">Connecting to Simulation Engine...</div>;
  if (isError || !currentSimulation) return <div className="min-h-screen bg-slate-950 flex items-center justify-center text-red-500">Failed to load simulation</div>;

  const progressPercent = Math.round((currentSimulation.currentRound / currentSimulation.totalRounds) * 100) || 0;

  const handlePause = async () => {
    await api.simulations.pause(simulationId);
    setCurrentSimulation({ ...currentSimulation, status: 'Paused' });
  };
  
  const handleResume = async () => {
    await api.simulations.resume(simulationId);
    setCurrentSimulation({ ...currentSimulation, status: 'Running' });
  };
  
  const handleStop = async () => {
    await api.simulations.stop(simulationId);
    setCurrentSimulation({ ...currentSimulation, status: 'Completed' });
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 flex flex-col h-screen overflow-hidden">
      
      {/* Header */}
      <header className="border-b border-slate-800 bg-slate-900 px-6 py-4 flex-none">
        <div className="flex justify-between items-center mb-4">
          <div className="flex items-center gap-4">
            <Link href="/">
              <Button variant="ghost" size="icon" className="text-slate-400 hover:text-white hover:bg-slate-800">
                <ArrowLeftIcon className="w-5 h-5" />
              </Button>
            </Link>
            <div>
              <div className="flex items-center gap-3 mb-1">
                <h1 className="text-xl font-semibold text-white">Simulation</h1>
                <span className="text-sm text-slate-500 font-mono">{currentSimulation.id}</span>
                <Badge variant={currentSimulation.status === 'Running' ? 'default' : 'secondary'} className={
                  currentSimulation.status === 'Running' ? 'bg-green-500 hover:bg-green-600' : ''
                }>
                  {currentSimulation.status}
                </Badge>
              </div>
              <div className="text-sm text-slate-400 max-w-2xl truncate">
                Query: <span className="text-slate-300 italic">"{currentSimulation.predictionQuery}"</span>
              </div>
            </div>
          </div>
          
          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-sm font-medium text-slate-200">
                Round {currentSimulation.currentRound} of {currentSimulation.totalRounds}
              </div>
              <div className="text-xs text-slate-500">{progressPercent}% Complete</div>
            </div>
            {currentSimulation.status === 'Completed' && (
              <Link href={`/simulations/${simulationId}/report`}>
                <Button className="bg-purple-600 hover:bg-purple-700 text-white shadow-[0_0_15px_rgba(147,51,234,0.3)]">
                  <FileTextIcon className="w-4 h-4 mr-2" />
                  View Report
                </Button>
              </Link>
            )}
          </div>
        </div>
        
        <Progress value={progressPercent} className="h-1 bg-slate-800" indicatorClassName={
          currentSimulation.status === 'Running' ? 'bg-blue-500 duration-1000' : 
          currentSimulation.status === 'Completed' ? 'bg-purple-500' : 'bg-amber-500'
        } />
      </header>

      {/* Main Grid */}
      <main className="flex-1 overflow-hidden p-6 gap-6 grid grid-cols-1 lg:grid-cols-3">
        
        {/* Left: Feed */}
        <div className="lg:col-span-2 h-full min-h-0">
          <AgentEventFeed simulationId={simulationId} />
        </div>
        
        {/* Right: Stats & Controls */}
        <div className="h-full flex flex-col gap-6 min-h-0">
          
          {/* Controls */}
          <div className="bg-slate-900 border border-slate-800 rounded-lg p-5">
            <h3 className="text-sm font-medium text-slate-400 uppercase tracking-wider mb-4">Simulation Controls</h3>
            <div className="grid grid-cols-3 gap-3">
              <Button 
                variant="outline" 
                className="bg-slate-950 border-slate-800 hover:bg-slate-800 hover:text-amber-400"
                onClick={handlePause}
                disabled={currentSimulation.status !== 'Running'}
              >
                <PauseIcon className="w-4 h-4 mr-1.5" /> Pause
              </Button>
              <Button 
                variant="outline" 
                className="bg-slate-950 border-slate-800 hover:bg-slate-800 hover:text-green-400"
                onClick={handleResume}
                disabled={currentSimulation.status === 'Running' || currentSimulation.status === 'Completed'}
              >
                <PlayIcon className="w-4 h-4 mr-1.5" /> Resume
              </Button>
              <Button 
                variant="outline" 
                className="bg-slate-950 border-slate-800 hover:bg-red-900/30 hover:text-red-400 hover:border-red-900/50"
                onClick={handleStop}
                disabled={currentSimulation.status === 'Completed'}
              >
                <SquareIcon className="w-4 h-4 mr-1.5 fill-current" /> Stop
              </Button>
            </div>
          </div>
          
          {/* Stats */}
          <div className="bg-slate-900 border border-slate-800 rounded-lg p-5 flex-1 overflow-y-auto">
            <h3 className="text-sm font-medium text-slate-400 uppercase tracking-wider mb-6">Real-Time Metrics</h3>
            
            <div className="space-y-6">
              <div>
                <div className="text-xs text-slate-500 mb-1">Active Agents</div>
                <div className="text-2xl font-light text-blue-400">{currentSimulation.agentCount.toLocaleString()}</div>
              </div>
              
              <div>
                <div className="text-xs text-slate-500 mb-2">Diversity Score</div>
                <div className="flex items-end gap-3">
                  <div className="text-2xl font-light text-purple-400">8.7</div>
                  <div className="text-xs text-slate-500 mb-1 pb-0.5">/ 10.0</div>
                </div>
                <Progress value={87} className="h-1.5 mt-2 bg-slate-950" indicatorClassName="bg-purple-500" />
              </div>

              <div>
                <div className="text-xs text-slate-500 mb-3">Event Distribution</div>
                <div className="space-y-3">
                  <div className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2"><div className="w-2 h-2 rounded-full bg-blue-500" /> Spoke</div>
                    <div className="text-slate-400">42%</div>
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2"><div className="w-2 h-2 rounded-full bg-amber-500" /> Reacted</div>
                    <div className="text-slate-400">28%</div>
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2"><div className="w-2 h-2 rounded-full bg-teal-500" /> Moved</div>
                    <div className="text-slate-400">19%</div>
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2"><div className="w-2 h-2 rounded-full bg-gray-500" /> Silent</div>
                    <div className="text-slate-400">11%</div>
                  </div>
                </div>
              </div>
              
              <div>
                <div className="text-xs text-slate-500 mb-1">Avg Response Time</div>
                <div className="text-xl font-light text-slate-200">24ms</div>
              </div>
            </div>
            
          </div>
        </div>
      </main>
    </div>
  );
}
