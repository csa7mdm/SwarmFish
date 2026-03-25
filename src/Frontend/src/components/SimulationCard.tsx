import { SimulationSummary } from '@/types/api';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { ActivityIcon, UsersIcon, CalendarIcon, FileTextIcon } from 'lucide-react';

interface SimulationCardProps {
  simulation: SimulationSummary;
}

const getStatusColor = (status: string) => {
  switch (status) {
    case 'Running': return 'bg-green-500/10 text-green-500 border-green-500/20';
    case 'Paused': return 'bg-amber-500/10 text-amber-500 border-amber-500/20';
    case 'Completed': return 'bg-blue-500/10 text-blue-500 border-blue-500/20';
    case 'Failed': return 'bg-red-500/10 text-red-500 border-red-500/20';
    default: return 'bg-slate-500/10 text-slate-400 border-slate-500/20';
  }
};

export function SimulationCard({ simulation }: SimulationCardProps) {
  const progress = Math.round((simulation.currentRound / simulation.totalRounds) * 100) || 0;
  
  return (
    <Card className="bg-slate-900 border-slate-800 hover:border-slate-700 transition-all group overflow-hidden shadow-lg hover:shadow-blue-900/10">
      <CardHeader className="p-4 pb-2">
        <div className="flex justify-between items-start">
          <Badge variant="outline" className={`font-medium ${getStatusColor(simulation.status)}`}>
            {simulation.status}
          </Badge>
          <span className="text-[10px] text-slate-500 font-mono">{simulation.id.slice(0, 8)}</span>
        </div>
      </CardHeader>
      
      <CardContent className="p-4 pt-2 space-y-4">
        <div>
          <h3 className="text-slate-200 font-medium line-clamp-1 group-hover:text-white transition-colors">
            {simulation.seedDocumentTitle}
          </h3>
          <div className="flex items-center text-xs text-slate-500 mt-1 gap-3">
             <span className="flex items-center gap-1">
               <UsersIcon className="w-3 h-3" /> {simulation.agentCount.toLocaleString()}
             </span>
             <span className="flex items-center gap-1">
               <CalendarIcon className="w-3 h-3" /> {new Date(simulation.createdAt).toLocaleDateString()}
             </span>
          </div>
        </div>

        <div className="space-y-1.5">
          <div className="flex justify-between text-[11px]">
            <span className="text-slate-400">Round {simulation.currentRound}/{simulation.totalRounds}</span>
            <span className="text-slate-200 font-medium">{progress}%</span>
          </div>
          <Progress value={progress} className="h-1 bg-slate-950" indicatorClassName="bg-blue-500 shadow-[0_0_8px_rgba(59,130,246,0.5)]" />
        </div>
      </CardContent>
    </Card>
  );
}
