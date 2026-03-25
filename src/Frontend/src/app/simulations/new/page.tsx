"use client"

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { SeedUploader, UploadedFile } from '@/components/SeedUploader';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Slider } from '@/components/ui/slider';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { CheckIcon, ChevronRightIcon, PlayIcon, ArrowLeftIcon, Loader2Icon } from 'lucide-react';
import { api } from '@/lib/api';
import { SimulationConfig } from '@/types/api';
import Link from 'next/link';

export default function NewSimulationPage() {
  const router = useRouter();
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [isLaunching, setIsLaunching] = useState(false);
  
  // Simulation config state
  const [seedFile, setSeedFile] = useState<UploadedFile | null>(null);
  const [seedId, setSeedId] = useState<string>('');
  const [agentCount, setAgentCount] = useState<number>(500);
  const [maxRounds, setMaxRounds] = useState<number>(100);
  const [predictionQuery, setPredictionQuery] = useState<string>('');
  const [mode, setMode] = useState<'Standard' | 'HighFidelity' | 'FastSweep'>('Standard');

  const handleUploadComplete = async (fileInfo: UploadedFile) => {
    setSeedFile(fileInfo);
    setSeedId(fileInfo.id);
    // Auto-advance after a short delay
    setTimeout(() => setStep(2), 1500);
  };

  const handleLaunch = async () => {
    try {
      setIsLaunching(true);
      const config: SimulationConfig = {
        seedDocumentId: seedId,
        agentCount,
        maxRounds,
        predictionQuery,
        mode
      };
      
      const { simulationId } = await api.simulations.start(config);
      router.push(`/simulations/${simulationId}`);
    } catch (error) {
      console.error("Failed to start simulation", error);
      setIsLaunching(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 p-6 md:p-12 font-sans selection:bg-blue-500/30">
      <div className="max-w-3xl mx-auto space-y-8">
        
        {/* Header */}
        <div className="flex items-center gap-4 border-b border-slate-800 pb-6">
          <Link href="/">
            <Button variant="ghost" size="icon" className="hover:bg-slate-900 text-slate-400 hover:text-slate-100">
              <ArrowLeftIcon className="w-5 h-5" />
            </Button>
          </Link>
          <div>
            <h1 className="text-2xl font-semibold text-white tracking-tight">Launch Simulation</h1>
            <p className="text-slate-400 text-sm mt-1">Configure parameters and spawn an agent swarm.</p>
          </div>
        </div>

        {/* Stepper */}
        <div className="flex items-center justify-between mb-8">
          {[1, 2, 3].map((num) => (
            <div key={num} className="flex items-center">
              <div className={`w-10 h-10 rounded-full flex items-center justify-center text-sm font-medium transition-all ${
                step === num 
                  ? 'bg-blue-600 text-white shadow-[0_0_15px_rgba(37,99,235,0.5)]' 
                  : step > num 
                    ? 'bg-green-500/20 text-green-400 border border-green-500/30' 
                    : 'bg-slate-900 text-slate-500 border border-slate-800'
              }`}>
                {step > num ? <CheckIcon className="w-5 h-5" /> : num}
              </div>
              <div className={`ml-3 mr-4 text-sm font-medium ${step === num ? 'text-slate-200' : 'text-slate-500'}`}>
                {num === 1 ? 'Seed Document' : num === 2 ? 'Configuration' : 'Review'}
              </div>
              {num < 3 && (
                <div className={`w-12 h-px mx-2 ${step > num ? 'bg-green-500/30' : 'bg-slate-800'}`} />
              )}
            </div>
          ))}
        </div>

        {/* Step 1: Upload */}
        {step === 1 && (
          <div className="animate-in fade-in slide-in-from-right-4 duration-300">
            <SeedUploader onUploadComplete={handleUploadComplete} />
            <div className="flex justify-end mt-6">
              <Button 
                onClick={() => setStep(2)} 
                disabled={!seedFile}
                className="bg-blue-600 hover:bg-blue-700 text-white border-0 shadow-[0_0_20px_rgba(37,99,235,0.2)]"
              >
                Next Step
                <ChevronRightIcon className="w-4 h-4 ml-1.5" />
              </Button>
            </div>
          </div>
        )}

        {/* Step 2: Configure */}
        {step === 2 && (
          <div className="animate-in fade-in slide-in-from-right-4 duration-300 space-y-8">
            <Card className="bg-slate-900/50 border-slate-800 shadow-xl shadow-black/20">
              <CardContent className="p-6 md:p-8 space-y-8">
                
                <div className="space-y-4">
                  <div className="flex justify-between">
                    <Label className="text-base text-slate-200 font-medium">Prediction Query</Label>
                  </div>
                  <Textarea 
                    placeholder="e.g., How will the market react to this earnings report over the next 3 weeks?"
                    className="min-h-[120px] bg-slate-950 border-slate-800 focus-visible:ring-blue-500/50 text-slate-200 resize-none"
                    value={predictionQuery}
                    onChange={(e) => setPredictionQuery(e.target.value)}
                  />
                  <p className="text-xs text-slate-500">The specific question you want the swarm to find an answer for.</p>
                </div>

                <div className="space-y-6 pt-4 border-t border-slate-800/50">
                  <div className="space-y-4">
                    <div className="flex justify-between items-center">
                      <Label className="text-slate-200 font-medium tracking-wide">Agent Count</Label>
                      <div className="px-3 py-1 bg-blue-500/10 border border-blue-500/20 rounded-md text-blue-400 font-mono text-sm">
                        {agentCount.toLocaleString()}
                      </div>
                    </div>
                    <Slider 
                      value={[agentCount]} 
                      min={10} 
                      max={10000} 
                      step={10}
                      onValueChange={(v) => setAgentCount(v[0])}
                      className="py-4"
                    />
                  </div>

                  <div className="space-y-4">
                    <div className="flex justify-between items-center">
                      <Label className="text-slate-200 font-medium tracking-wide">Max Rounds</Label>
                      <div className="px-3 py-1 bg-amber-500/10 border border-amber-500/20 rounded-md text-amber-400 font-mono text-sm">
                        {maxRounds}
                      </div>
                    </div>
                    <Slider 
                      value={[maxRounds]} 
                      min={10} 
                      max={500} 
                      step={5}
                      onValueChange={(v) => setMaxRounds(v[0])}
                      className="py-4"
                    />
                  </div>
                </div>

              </CardContent>
            </Card>

            <div className="flex justify-between items-center">
              <Button variant="ghost" onClick={() => setStep(1)} className="text-slate-400 hover:text-white hover:bg-slate-900">
                Back
              </Button>
              <Button 
                onClick={() => setStep(3)} 
                disabled={predictionQuery.length < 10}
                className="bg-blue-600 hover:bg-blue-700 text-white shadow-[0_0_20px_rgba(37,99,235,0.3)] transition-all"
              >
                Review Configuration
                <ChevronRightIcon className="w-4 h-4 ml-1.5" />
              </Button>
            </div>
          </div>
        )}

        {/* Step 3: Review */}
        {step === 3 && (
          <div className="animate-in fade-in slide-in-from-right-4 duration-300 space-y-8">
            <Card className="bg-slate-900 border-slate-800 shadow-2xl overflow-hidden relative">
              <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-blue-500 via-purple-500 to-green-500" />
              <CardContent className="p-8">
                <h3 className="text-xl font-medium text-white mb-6">Ready to launch</h3>
                
                <div className="grid grid-cols-2 gap-y-6 gap-x-12">
                  <div className="space-y-1">
                    <div className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Seed Document</div>
                    <div className="text-slate-200 font-medium truncate" title={seedFile?.name}>
                      {seedFile?.name || 'Unknown'}
                    </div>
                  </div>
                  
                  <div className="space-y-1">
                    <div className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Simulation Mode</div>
                    <div className="text-slate-200 font-medium">{mode}</div>
                  </div>
                  
                  <div className="space-y-1">
                    <div className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Swarm Size</div>
                    <div className="text-blue-400 font-medium">{agentCount.toLocaleString()} Agents</div>
                  </div>
                  
                  <div className="space-y-1">
                    <div className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Duration Limit</div>
                    <div className="text-amber-400 font-medium">{maxRounds} Rounds</div>
                  </div>
                  
                  <div className="col-span-2 space-y-2 mt-2 pt-6 border-t border-slate-800/60">
                    <div className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Target Prediction</div>
                    <div className="p-4 bg-slate-950/50 rounded-lg border border-slate-800 text-slate-300 italic">
                      "{predictionQuery}"
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>

            <div className="flex justify-between items-center">
              <Button variant="ghost" onClick={() => setStep(2)} className="text-slate-400 hover:text-white hover:bg-slate-900">
                Back to Config
              </Button>
              <Button 
                onClick={handleLaunch} 
                disabled={isLaunching}
                size="lg"
                className="bg-green-600 hover:bg-green-700 text-white font-medium border-0 shadow-[0_0_30px_rgba(22,163,74,0.4)] transition-all"
              >
                {isLaunching ? (
                  <>
                    <Loader2Icon className="w-5 h-5 mr-2 animate-spin" />
                    Initializing Agents...
                  </>
                ) : (
                  <>
                    <PlayIcon className="w-5 h-5 mr-2 fill-current" />
                    Launch Simulation
                  </>
                )}
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
