"use client"

import { useEffect, useState, useRef } from 'react';
import { useParams } from 'next/navigation';
import { api } from '@/lib/api';
import { PredictionReport, ChatMessage } from '@/types/api';
import { ConfidenceBar } from '@/components/ConfidenceBar';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';
import { ArrowLeftIcon, SendIcon, BotIcon, UserIcon, FileCheckIcon } from 'lucide-react';
import Link from 'next/link';

export default function ReportPage() {
  const params = useParams();
  const simulationId = params.id as string;
  
  const [report, setReport] = useState<PredictionReport | null>(null);
  const [chatHistory, setChatHistory] = useState<ChatMessage[]>([]);
  const [inputMsg, setInputMsg] = useState('');
  const [isChatting, setIsChatting] = useState(false);
  
  const chatScrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    api.reports.get(simulationId).then(setReport);
    
    // Initial greeting
    setChatHistory([
      { role: 'assistant', content: 'Hello. I am the ReportAgent for this simulation. I can answer detailed questions about the swarm\'s behavior, consensus formation, or dissenting views. What would you like to know?', timestamp: new Date().toISOString() }
    ]);
  }, [simulationId]);

  useEffect(() => {
    if (chatScrollRef.current) {
      const scrollContainer = chatScrollRef.current.querySelector('[data-radix-scroll-area-viewport]');
      if (scrollContainer) {
        scrollContainer.scrollTop = scrollContainer.scrollHeight;
      }
    }
  }, [chatHistory]);

  const handleSendMessage = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputMsg.trim() || isChatting) return;
    
    const newMsg: ChatMessage = { role: 'user', content: inputMsg, timestamp: new Date().toISOString() };
    const newHistory = [...chatHistory, newMsg];
    
    setChatHistory(newHistory);
    setInputMsg('');
    setIsChatting(true);
    
    try {
      const response = await api.reports.chat(simulationId, newMsg.content, newHistory);
      setChatHistory([...newHistory, { role: 'assistant', content: response, timestamp: new Date().toISOString() }]);
    } finally {
      setIsChatting(false);
    }
  };

  if (!report) return <div className="min-h-screen bg-slate-950 flex items-center justify-center text-slate-500">Loading Report...</div>;

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 flex flex-col h-screen overflow-hidden">
      
      {/* Header */}
      <header className="border-b border-slate-800 bg-slate-900 px-6 py-4 flex-none flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Link href={`/simulations/${simulationId}`}>
            <Button variant="ghost" size="icon" className="text-slate-400 hover:text-white hover:bg-slate-800">
              <ArrowLeftIcon className="w-5 h-5" />
            </Button>
          </Link>
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-purple-500 to-indigo-600 flex items-center justify-center shadow-lg shadow-purple-900/30">
              <FileCheckIcon className="w-5 h-5 text-white" />
            </div>
            <div>
              <h1 className="text-xl font-semibold text-white">Prediction Report</h1>
              <div className="text-xs text-slate-500 font-mono tracking-widest uppercase">SIM: {simulationId}</div>
            </div>
          </div>
        </div>
      </header>

      {/* Main Grid */}
      <main className="flex-1 overflow-hidden p-6 gap-6 grid grid-cols-1 xl:grid-cols-5">
        
        {/* Left: Structured Report (takes up 3 cols) */}
        <div className="xl:col-span-3 h-full min-h-0 overflow-y-auto pr-2 custom-scrollbar space-y-8 pb-12">
          
          <Card className="bg-slate-900/50 border-slate-800 border-t-4 border-t-purple-500">
            <CardHeader>
              <CardTitle className="text-xl text-white">Executive Summary</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-slate-300 leading-relaxed bg-slate-950/50 p-6 rounded-lg border border-slate-800/80 shadow-[inset_0_2px_4px_rgba(0,0,0,0.5)]">
                {report.summary}
              </p>
              
              <div className="mt-8 pt-6 border-t border-slate-800">
                <h4 className="text-sm font-medium uppercase tracking-wider text-slate-500 mb-6">Overall Swarm Confidence</h4>
                <div className="max-w-md">
                  <ConfidenceBar score={report.confidenceScore} label="Prediction Certainty" />
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <Card className="bg-slate-900 border-slate-800">
              <CardHeader>
                <CardTitle className="text-lg text-white">Key Findings</CardTitle>
              </CardHeader>
              <CardContent className="space-y-6">
                {report.findings.filter(f => f.category !== 'Dissenting view').map((finding, idx) => (
                  <div key={idx} className="space-y-3">
                    <div className="text-xs font-semibold uppercase text-blue-400 tracking-wider">{finding.category}</div>
                    <p className="text-sm text-slate-300">{finding.description}</p>
                    <ConfidenceBar score={finding.confidence} label="Consensus Level" />
                  </div>
                ))}
              </CardContent>
            </Card>

            <div className="space-y-6">
              <Card className="bg-slate-900 border-slate-800 border-l-4 border-l-amber-500 h-1/2">
                <CardHeader className="pb-3">
                  <CardTitle className="text-lg text-white">Dissenting Views</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  {report.findings.filter(f => f.category === 'Dissenting view').map((finding, idx) => (
                    <div key={idx} className="space-y-2">
                      <p className="text-sm text-slate-300 italic">"{finding.description}"</p>
                      <div className="flex justify-between items-center text-xs">
                        <span className="text-amber-500 font-medium">Minority Cohort</span>
                        <span className="text-slate-500">Support: {Math.round(finding.confidence * 100)}%</span>
                      </div>
                    </div>
                  ))}
                  {report.findings.filter(f => f.category === 'Dissenting view').length === 0 && (
                     <p className="text-sm text-slate-500">No significant dissenting views recorded.</p>
                  )}
                </CardContent>
              </Card>

              <Card className="bg-slate-900 border-slate-800 h-[calc(50%-1.5rem)]">
                <CardHeader className="pb-3">
                  <CardTitle className="text-lg text-white">Simulation Timeline</CardTitle>
                </CardHeader>
                <CardContent className="space-y-6">
                  {report.timeline.map((event, idx) => (
                    <div key={idx} className="relative pl-6 border-l-2 border-slate-800 pb-2 last:border-0 last:pb-0">
                      <div className="absolute top-0 left-[-5px] w-2 h-2 rounded-full bg-blue-500 ring-4 ring-slate-900" />
                      <div className="text-xs text-slate-500 mb-1">{new Date(event.simulatedAt).toLocaleTimeString()}</div>
                      <p className="text-sm text-slate-300">{event.description}</p>
                    </div>
                  ))}
                </CardContent>
              </Card>
            </div>
          </div>
          
        </div>
        
        {/* Right: Chat Interface (takes up 2 cols) */}
        <div className="xl:col-span-2 h-full min-h-0 flex flex-col bg-slate-900 border border-slate-800 rounded-xl overflow-hidden shadow-2xl">
          <div className="px-5 py-4 border-b border-slate-800 bg-slate-950 flex items-center gap-3">
            <div className="w-2 h-2 rounded-full bg-green-500 shadow-[0_0_8px_theme(colors.green.500)]" />
            <h3 className="font-medium text-slate-200">ReportAgent</h3>
          </div>
          
          <ScrollArea className="flex-1 p-5" ref={chatScrollRef}>
            <div className="space-y-6 pr-4">
              {chatHistory.map((msg, i) => (
                <div key={i} className={`flex gap-3 max-w-[90%] ${msg.role === 'user' ? 'ml-auto flex-row-reverse' : ''}`}>
                  <div className={`w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 ${
                    msg.role === 'assistant' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-400'
                  }`}>
                    {msg.role === 'assistant' ? <BotIcon className="w-4 h-4" /> : <UserIcon className="w-4 h-4" />}
                  </div>
                  <div className={`rounded-xl px-4 py-3 text-sm leading-relaxed ${
                    msg.role === 'assistant' 
                      ? 'bg-slate-800 text-slate-200 rounded-tl-sm' 
                      : 'bg-blue-600 text-white rounded-tr-sm'
                  }`}>
                    {msg.content}
                  </div>
                </div>
              ))}
              {isChatting && (
                 <div className="flex gap-3 max-w-[90%]">
                 <div className="w-8 h-8 rounded-full bg-indigo-600 text-white flex items-center justify-center flex-shrink-0">
                   <BotIcon className="w-4 h-4" />
                 </div>
                 <div className="rounded-xl px-4 py-3 bg-slate-800 rounded-tl-sm flex items-center gap-1">
                   <div className="w-1.5 h-1.5 rounded-full bg-slate-500 animate-bounce" style={{ animationDelay: '0ms' }} />
                   <div className="w-1.5 h-1.5 rounded-full bg-slate-500 animate-bounce" style={{ animationDelay: '150ms' }} />
                   <div className="w-1.5 h-1.5 rounded-full bg-slate-500 animate-bounce" style={{ animationDelay: '300ms' }} />
                 </div>
               </div>
              )}
            </div>
          </ScrollArea>
          
          <div className="p-4 border-t border-slate-800 bg-slate-950">
            <form onSubmit={handleSendMessage} className="relative">
              <Input 
                value={inputMsg}
                onChange={e => setInputMsg(e.target.value)}
                placeholder="Ask about the simulation results..."
                className="bg-slate-900 border-slate-800 pr-12 focus-visible:ring-indigo-500/50"
                disabled={isChatting}
              />
              <Button 
                type="submit" 
                size="icon"
                disabled={isChatting || !inputMsg.trim()}
                className="absolute right-1 top-1 bottom-1 h-auto bg-indigo-600 hover:bg-indigo-700 text-white border-0 w-8"
              >
                <SendIcon className="w-3.5 h-3.5" />
              </Button>
            </form>
          </div>
        </div>
      </main>
    </div>
  );
}
