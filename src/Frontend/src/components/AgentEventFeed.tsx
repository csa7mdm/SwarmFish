"use client"

import { useEffect, useRef } from 'react';
import { useSimulationStore } from '@/stores/simulation';
import { Badge } from '@/components/ui/badge';
import { ScrollArea } from '@/components/ui/scroll-area';
import { BotIcon } from 'lucide-react';
import { AgentEvent } from '@/types/api';

interface AgentEventFeedProps {
  simulationId: string;
}

const getEventTypeColor = (type: string) => {
  const normType = type.toLowerCase();
  if (normType === 'spoke') return 'bg-blue-500/20 text-blue-400 border-blue-500/30';
  if (normType === 'reacted') return 'bg-amber-500/20 text-amber-400 border-amber-500/30';
  if (normType === 'silent') return 'bg-gray-500/20 text-gray-400 border-gray-500/30';
  if (normType === 'moved') return 'bg-teal-500/20 text-teal-400 border-teal-500/30';
  return 'bg-slate-500/20 text-slate-400 border-slate-500/30';
};

const formatTime = (isoString: string) => {
  const date = new Date(isoString);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
};

export function AgentEventFeed({ simulationId }: AgentEventFeedProps) {
  const events = useSimulationStore((state) => state.events);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // Auto-scroll to bottom when new events arrive
    if (scrollRef.current) {
      const scrollContainer = scrollRef.current.querySelector('[data-radix-scroll-area-viewport]');
      if (scrollContainer) {
        scrollContainer.scrollTop = scrollContainer.scrollHeight;
      }
    }
  }, [events]);

  return (
    <div className="flex flex-col h-full bg-slate-950 border border-slate-800 rounded-lg overflow-hidden">
      <div className="px-4 py-3 border-b border-slate-800 bg-slate-900/50 flex justify-between items-center">
        <h3 className="font-medium text-slate-200">Live Agent Feed</h3>
        <Badge variant="outline" className="text-xs bg-slate-900 border-slate-700">
          {events.length} Events (Max 500)
        </Badge>
      </div>
      
      <ScrollArea className="flex-1 p-4" ref={scrollRef}>
        <div className="space-y-4">
          {events.length === 0 ? (
            <div className="text-center text-slate-500 text-sm py-8">
              Waiting for events from simulation {simulationId.substring(0, 8)}...
            </div>
          ) : (
            events.map((event, i) => (
              <div key={`${event.timestamp}-${i}`} className="flex gap-3 text-sm animate-in slide-in-from-bottom-2 fade-in duration-200">
                <div className="flex-shrink-0 mt-0.5">
                  <div className="w-8 h-8 rounded-full bg-slate-800 flex items-center justify-center border border-slate-700">
                    <BotIcon className="w-4 h-4 text-slate-400" />
                  </div>
                </div>
                <div className="flex-1 space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-slate-300">
                      Agent-{event.agentId.substring(0, 6)}
                    </span>
                    <Badge variant="outline" className={`text-[10px] h-5 px-1.5 ${getEventTypeColor(event.eventType)}`}>
                      {event.eventType}
                    </Badge>
                    <span className="text-xs text-slate-500 ml-auto">
                      {formatTime(event.timestamp)}
                    </span>
                  </div>
                  <div className="text-slate-400 text-sm leading-relaxed p-2 bg-slate-900/50 rounded-md border border-slate-800">
                    {event.payload}
                  </div>
                </div>
              </div>
            ))
          )}
        </div>
      </ScrollArea>
    </div>
  );
}
